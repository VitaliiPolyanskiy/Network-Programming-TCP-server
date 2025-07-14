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
            // Получим контекст синхронизации для текущего потока 
            uiContext = SynchronizationContext.Current;
        }

        /*
                                                
        SOCKET – это некоторое логическое гнездо, которое позволяет двум приложениям обмениватся информацией 
        по сети не задумываяся о месте расположения. SOCKET – это комбинация IP-address и номера порта.
        
        Internet Protocol (IP) - широко используемый протокол как в локальных, так и в глобальных сетях.
        Этот протокол не требует установления соединения и не гарантирует доставку данных. Поэтому для
        передачи данных поверх IP используются два протокола более высокого уровня: TCP, UDP.
        
        Transmission Control Protocol (TCP) реализует связь с установлением соединения, обеспечивая 
        безошибочную передачу данных между компьютерами. 
        
        Связь без установления соединения выполняется при помощи User Datagram Protocol (UDP). Не гарантируя
        надёжности, UDP может осуществлять передачу данных множеству адресатов и принимать данные от множества
        источников. Например, данные, отправляемые клиентом на сервер, передаются немедленно, независимо от того,
        готов ли сервер к приёму. При получении данных от клиента, сервер не подтверждает их приём. Данные 
        передаются в виде дейтаграмм. И TCP, и UDP передают данные по IP, поэтому обычно говорят об использовании 
        TCP/IP или UDP/IP.
        */

        // обслуживание очередного запроса будем выполнять в отдельном потоке
        private async void Receive(TcpClient tcpClient)
        {
            await Task.Run(async() =>
            {
                NetworkStream netstream = null;
                try
                {
                    // Получим объект NetworkStream, используемый для приема и передачи данных.
                    netstream = tcpClient.GetStream();
                    string client = null;
                    byte[] arr = new byte[tcpClient.ReceiveBufferSize /* размер приемного буфера */];
                    // Читаем данные из объекта NetworkStream.
                    int len = await netstream.ReadAsync(arr, 0, tcpClient.ReceiveBufferSize);// Возвращает фактически считанное число байтов
                    client = Encoding.Default.GetString(arr, 0, len); // конвертируем массив байтов в строку
                    while (true)
                    {
                        len = await netstream.ReadAsync(arr, 0, tcpClient.ReceiveBufferSize);// Возвращает фактически считанное число байтов
                        if (len == 0)
                        {
                            netstream.Close();
                            tcpClient.Close(); // закрываем TCP-подключение и освобождаем все ресурсы, связанные с объектом TcpClient.
                            return;
                        }
                        // Создадим поток, резервным хранилищем которого является память.
                        //byte[] copy = new byte[len];
                        //Array.Copy(arr, 0, copy, 0, len);
                        MemoryStream stream = new MemoryStream(arr, 0, len);
                        var jsonFormatter = new DataContractJsonSerializer(typeof(MessageTCP));
                        MessageTCP m = jsonFormatter.ReadObject(stream) as MessageTCP;// выполняем десериализацию

                        // полученную от клиента информацию добавляем в список
                        string Result = m.Host + " - " + m.User + " - " + m.Message;
                        // uiContext.Send отправляет синхронное сообщение в контекст синхронизации
                        // SendOrPostCallback - делегат указывает метод, вызываемый при отправке сообщения в контекст синхронизации. 
                        uiContext.Send(d => listBox1.Items.Add(Result) /* Вызываемый делегат SendOrPostCallback */,
                            null /* Объект, переданный делегату */); // добавляем в список имя клиента
                        stream.Close();
                        if (m.Message.IndexOf("<end>") > -1) // если клиент отправил эту команду, то заканчиваем обработку сообщений
                            break;
                    }
                    string theReply = "Я завершаю обработку сообщений";
                    byte[] msg = Encoding.Default.GetBytes(theReply); // конвертируем строку в массив байтов
                    await netstream.WriteAsync(msg, 0, msg.Length); // записываем данные в NetworkStream.
                    netstream.Close();
                    tcpClient.Close(); // закрываем TCP-подключение и освобождаем все ресурсы, связанные с объектом TcpClient.
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Сервер: " + ex.Message);
                    netstream?.Close();
                    tcpClient?.Close(); // закрываем TCP-подключение и освобождаем все ресурсы, связанные с объектом TcpClient.
                }
            });
        }

        //  ожидать запросы на соединение будем в отдельном потоке
        private async void Accept()
        {
            await Task.Run(async() =>
            {
                try
                {
                    // TcpListener ожидает подключения от TCP-клиентов сети.
                    TcpListener listener = new TcpListener(
                    IPAddress.Any /* Предоставляет IP-адрес, указывающий, что сервер должен контролировать действия клиентов на всех сетевых интерфейсах.*/,
                    49152 /* порт */);
                    listener.Start(); // Запускаем ожидание входящих запросов на подключение
                    while (true)
                    {
                        // Принимаем ожидающий запрос на подключение 
                        // Метод AcceptTcpClient — это блокирующий метод, возвращающий объект TcpClient, 
                        // который может использоваться для приема и передачи данных.
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
