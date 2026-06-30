using System;
using System.Text;
using System.Windows.Forms;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using ClassLibrary_Message;
using System.Runtime.Serialization.Json;

namespace socket_TCP_simple
{
    public partial class Form1 : Form
    {
        public SynchronizationContext uiContext;
        public Form1()
        {
            InitializeComponent();
            // Отримаємо контекст синхронізації для поточного потоку 
            uiContext = SynchronizationContext.Current;
        }

        /*
        SOCKET – це деяке логічне гніздо, яке дозволяє двом додаткам обмінюватися інформацією 
        мережею, не замислюючись про місце розташування. SOCKET – це комбінація IP-address та номера порту.
        
        Internet Protocol (IP) - протокол, що широко використовується як у локальних, так і в глобальних мережах.
        Цей протокол не потребує встановлення з'єднання і не гарантує доставку даних. Тому для
        передачі даних поверх IP використовуються два протоколи вищого рівня: TCP, UDP.
        
        Transmission Control Protocol (TCP) реалізує зв'язок із встановленням з'єднання, забезпечуючи 
        безпомилкову передачу даних між комп'ютерами. 
        
        Зв'язок без встановлення з'єднання виконується за допомогою User Datagram Protocol (UDP). Не гарантуючи
        надійності, UDP може здійснювати передачу даних безлічі адресатів і приймати дані від безлічі
        джерел. Наприклад, дані, що відправляються клієнтом на сервер, передаються негайно, незалежно від того,
        чи готовий сервер до прийому. При отриманні даних від клієнта, сервер не підтверджує їхній прийом. Дані 
        передаються у вигляді дейтаграм. І TCP, і UDP передають дані по IP, тому зазвичай говорять про використання 
        TCP/IP або UDP/IP.
        */

        // Обслуговування чергового запиту виконуватимемо в окремому потоці
        private async void Receive(TcpClient tcpClient)
        {
            await Task.Run(async () =>
            {
                NetworkStream netstream = null;
                try
                {
                    // Отримаємо об'єкт NetworkStream, що використовується для прийому та передачі даних.
                    netstream = tcpClient.GetStream();
                    string client = null;
                    byte[] arr = new byte[tcpClient.ReceiveBufferSize /* розмір прийомного буфера */];
                    // Читаємо дані з об'єкта NetworkStream.
                    int len = await netstream.ReadAsync(arr, 0, tcpClient.ReceiveBufferSize);// Повертає фактично зчитану кількість байтів
                    client = Encoding.Default.GetString(arr, 0, len); // конвертуємо масив байтів у рядок
                    while (true)
                    {
                        len = await netstream.ReadAsync(arr, 0, tcpClient.ReceiveBufferSize);// Повертає фактично зчитану кількість байтів
                        if (len == 0)
                        {
                            netstream.Close();
                            tcpClient.Close(); // закриваємо TCP-підключення та звільняємо всі ресурси, пов'язані з об'єктом TcpClient.
                            return;
                        }
                        // Створимо потік, резервним сховищем якого є пам'ять.
                        //byte[] copy = new byte[len];
                        //Array.Copy(arr, 0, copy, 0, len);
                        MemoryStream stream = new MemoryStream(arr, 0, len);
                        var jsonFormatter = new DataContractJsonSerializer(typeof(MessageTCP));
                        MessageTCP m = jsonFormatter.ReadObject(stream) as MessageTCP;// виконуємо десеріалізацію

                        // отриману від клієнта інформацію додаємо до списку
                        string Result = m.Host + " - " + m.User + " - " + m.Message;
                        // uiContext.Send відправляє синхронне повідомлення в контекст синхронізації
                        // SendOrPostCallback - делегат вказує метод, що викликається при відправці повідомлення в контекст синхронізації. 
                        uiContext.Send(d => listBox1.Items.Add(Result) /* Викликаний делегат SendOrPostCallback */,
                            null /* Об'єкт, переданий делегату */); // додаємо до списку ім'я клієнта
                        stream.Close();
                        if (m.Message.IndexOf("<end>") > -1) // якщо клієнт відправив цю команду, то завершуємо обробку повідомлень
                            break;
                    }
                    string theReply = "Я завершую обробку повідомлень";
                    byte[] msg = Encoding.Default.GetBytes(theReply); // конвертируем рядок у масив байтів
                    await netstream.WriteAsync(msg, 0, msg.Length); // записуємо дані в NetworkStream.
                    netstream.Close();
                    tcpClient.Close(); // закриваємо TCP-підключення та звільняємо всі ресурси, пов'язані з об'єктом TcpClient.
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Сервер: " + ex.Message);
                    netstream?.Close();
                    tcpClient?.Close(); // закриваємо TCP-підключення та звільняємо всі ресурси, пов'язані з об'єктом TcpClient.
                }
            });
        }

        // Очікувати запити на з'єднання будемо в окремому потоці
        private async void Accept()
        {
            await Task.Run(async () =>
            {
                try
                {
                    // TcpListener очікує підключення від TCP-клієнтів мережі.
                    TcpListener listener = new TcpListener(
                    IPAddress.Any /* Надає IP-адресу, яка вказує, що сервер повинен контролювати дії клієнтів на всіх мережевих інтерфейсах.*/,
                    49152 /* порт */);
                    listener.Start(); // Запускаємо очікування вхідних запитів на підключення
                    while (true)
                    {
                        // Приймаємо запит на підключення, що очікує 
                        // Метод AcceptTcpClient — це блокуючий метод, який повертає об'єкт TcpClient, 
                        // що може використовуватися для прийому та передачі даних.
                        TcpClient client = await listener.AcceptTcpClientAsync();
                        Receive(client);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Сервер: " + ex.Message);
                }
            });
        }

        private void button1_Click(object sender, EventArgs e)
        {
            Accept();
        }
    }
}