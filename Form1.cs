using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using PCSC;
using PCSC.Iso7816;


namespace WindowsFormsApp1
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {

        }

        private void buttonCheckReader_Click(object sender, EventArgs e)
        {
            textBox_Log.Clear(); //①
            comboBox_CardReader.Items.Clear(); //②

            using (var ctx = ContextFactory.Instance.Establish(SCardScope.User))
            {
                var firstReader = ctx
                    .GetReaders()
                    .FirstOrDefault();

                if (firstReader == null)
                {
                    textBox_Log.Text += "No reader connected."; //③
                    return;
                }

                using (var reader = ctx.ConnectReader(firstReader, SCardShareMode.Direct, SCardProtocol.Unset))
                {
                    var status = reader.GetStatus();

                    textBox_Log.Text += @"Reader names: {" + string.Join(", ", status.GetReaderNames()) + "}\r\n"; //④以下4行
                    textBox_Log.Text += @"Protocol: {" + status.Protocol + "}\r\n";
                    textBox_Log.Text += @"State: {" + status.State + "}\r\n";
                    textBox_Log.Text += @"ATR: { " + BitConverter.ToString(status.GetAtr() ?? new byte[0]) + "}\r\n";
                    textBox_Log.Text += @"ATR: { " + NoBarATR(status.GetAtr()) + "}\r\n";

                    comboBox_CardReader.Items.AddRange(status.GetReaderNames());//⑤
                }
            }
            textBox_Log.Text += "-----------------------------------------------\r\n"; //⑥
        }

        /// <summary>
        /// ハイフンがない形のATRを出力します。
        /// </summary>
        /// <param name="arr"></param>
        /// <returns></returns>
        private string NoBarATR(byte[] arr)
        {
            if (arr == null) return string.Empty;

            string result = string.Empty;

            foreach (byte oneByte in arr)
            {
                result += String.Format("{0:X2}", oneByte) + " ";
            }

            return result;
        }

        private int _selectedIndex = -1;

        private void comboBox_CardReader_SelectedIndexChanged(object sender, EventArgs e)
        {
            _selectedIndex = comboBox_CardReader.SelectedIndex;
        }

        private void button_ReadID_Click(object sender, EventArgs e)
        {
            var contextFactory = ContextFactory.Instance;

            using (var context = contextFactory.Establish(SCardScope.System))
            {
                var readerNames = context.GetReaders();

                if (NoReaderFound(readerNames))
                {
                    textBox_Log.Text += "You need at least one reader in order to run this example." + "\r\n";
                    return;
                }

                var readerName = ChooseRfidReader(readerNames);
                if (readerName == null)
                {
                    textBox_Log.Text += readerName + "うまくいってない、readNamesがないみたいです\r\n";
                    return;
                }

                // 'using' statement to make sure the reader will be disposed (disconnected) on exit
                using (var rfidReader = context.ConnectReader(readerName, SCardShareMode.Shared, SCardProtocol.Any))
                {
                    var apdu = new CommandApdu(IsoCase.Case2Short, rfidReader.Protocol)
                    {
                        CLA = 0xFF,
                        Instruction = InstructionCode.GetData,
                        P1 = 0x00,
                        P2 = 0x00,
                        Le = 0 // We don't know the ID tag size
                    };

                    using (rfidReader.Transaction(SCardReaderDisposition.Leave))
                    {
                        textBox_Log.Text += "Retrieving the UID .... " + "\r\n";

                        var sendPci = SCardPCI.GetPci(rfidReader.Protocol);
                        var receivePci = new SCardPCI(); // IO returned protocol control information.

                        var receiveBuffer = new byte[256];
                        var command = apdu.ToArray();

                        var bytesReceived = rfidReader.Transmit(
                            sendPci, // Protocol Control Information (T0, T1 or Raw)
                            command, // command APDU
                            command.Length,
                            receivePci, // returning Protocol Control Information
                            receiveBuffer,
                            receiveBuffer.Length); // data buffer

                        var responseApdu =
                            new ResponseApdu(receiveBuffer, bytesReceived, IsoCase.Case2Short, rfidReader.Protocol);
                        textBox_Log.Text += "SW1: " + responseApdu.SW1.ToString()
                                                + ", SW2: " + responseApdu.SW2.ToString()
                                                + "\r\n";
                        if (responseApdu.HasData)
                        {
                            textBox_Log.Text += "Uid: " + BitConverter.ToString(responseApdu.GetData()) + "\r\n";
                        }
                        else
                        {
                            textBox_Log.Text += "Uid: No uid received" + "\r\n";
                        }

                    }
                }
            }
            textBox_Log.Text += "-----------------------------------------------\r\n";
        }

        /// <summary>
        /// Returns <c>true</c> if the supplied collection <paramref name="readerNames"/> does not contain any reader.
        /// </summary>
        /// <param name="readerNames">Collection of smartcard reader names</param>
        /// <returns><c>true</c> if no reader found</returns>
        private bool NoReaderFound(ICollection<string> readerNames)
        {
            if (readerNames == null) return true;
            if (readerNames.Count == 0) return true;

            return false;
        }

        public string ChooseRfidReader(IList<string> readerNames)
        {
            // Show available readers.
            // Console.WriteLine("Available readers: ");

            textBox_Log.Text += "Available readers: ";

            for (var i = 0; i < readerNames.Count; i++)
            {
                textBox_Log.Text += "ReaderID: " + i.ToString() + " ,Reader Name:" + readerNames[i] + "\r\n";
            }

            // Ask the user which one to choose.
            textBox_Log.Text += "Which reader is an RFID reader? \r\n";
            var line = "0";

            int choice = _selectedIndex;
            if (int.TryParse(line, out choice))
            {
                if (choice >= 0 && (choice <= readerNames.Count))
                {
                    return readerNames[choice];
                }
            }

            textBox_Log.Text += "An invalid number has been entered.\r\n";
            //Console.ReadKey();
            return null;
        }

        private void button_ReadCardType_Click(object sender, EventArgs e)
        {

        }

        private void button1_Click(object sender, EventArgs e)
        {
            var contextFactory = ContextFactory.Instance;

            using (var context = contextFactory.Establish(SCardScope.System))
            {
                var readerNames = context.GetReaders();
                if (NoReaderFound(readerNames))
                {
                    textBox_Log.Text += "You need at least one reader in order to run this example.\r\n";
                    return;
                }

                var readerName = ChooseRfidReader(readerNames);
                if (readerName == null)
                {
                    return;
                }

                // 'using' statement to make sure the reader will be disposed (disconnected) on exit
                using (var rfidReader = context.ConnectReader(readerName, SCardShareMode.Shared, SCardProtocol.Any))
                {

                    //① SelectFileで指定 
                    // Case4で設定します
                    byte[] dataIn = { 0x0f, 0x09 };

                    var apduSelectFile = new CommandApdu(IsoCase.Case4Short, rfidReader.Protocol)
                    {
                        CLA = 0xFF,
                        Instruction = InstructionCode.SelectFile,
                        P1 = 0x00,
                        P2 = 0x01,
                        // Lcは自動計算
                        Data = dataIn,
                        Le = 0 // 
                    };


                    using (rfidReader.Transaction(SCardReaderDisposition.Leave))
                    {
                        textBox_Log.Text += "SelectFile .... \r\n";

                        var sendPci = SCardPCI.GetPci(rfidReader.Protocol);
                        var receivePci = new SCardPCI(); // IO returned protocol control information.

                        var receiveBuffer = new byte[256];
                        var command = apduSelectFile.ToArray();

                        var bytesReceivedSelectedFile = rfidReader.Transmit(
                            sendPci, // Protocol Control Information (T0, T1 or Raw)
                            command, // command APDU
                            command.Length,
                            receivePci, // returning Protocol Control Information
                            receiveBuffer,
                            receiveBuffer.Length); // data buffer

                        var responseApdu =
                            new ResponseApdu(receiveBuffer, bytesReceivedSelectedFile, IsoCase.Case2Short, rfidReader.Protocol);


                        textBox_Log.Text += "SW1: " + responseApdu.SW1.ToString()
                                                + ", SW2: " + responseApdu.SW2.ToString() + "\r\n"
            + "Length: " + responseApdu.Length.ToString() + "\r\n";

                        for (int i = 0; i < 20; ++i)
                        {

                            //② ReadBinaryとブロック指定
                            //176 = 0xB0
                            var apduReadBinary = new CommandApdu(IsoCase.Case2Short, rfidReader.Protocol)
                            {
                                CLA = 0xFF,
                                Instruction = InstructionCode.ReadBinary,
                                P1 = 0x00,
                                P2 = (byte)i,
                                Le = 0 // 
                            };

                            //textBox_Log.Text += "Read Binary .... \r\n";

                            var commandReadBinary = apduReadBinary.ToArray();

                            var bytesReceivedReadBinary2 = rfidReader.Transmit(
                                sendPci, // Protocol Control Information (T0, T1 or Raw)
                                commandReadBinary, // command APDU
                                commandReadBinary.Length,
                                receivePci, // returning Protocol Control Information
                                receiveBuffer,
                                receiveBuffer.Length); // data buffer

                            var responseApdu2 =
                                new ResponseApdu(receiveBuffer, bytesReceivedReadBinary2, IsoCase.Case2Extended, rfidReader.Protocol);

                            /*textBox_Log.Text += "SW1: " + responseApdu2.SW1.ToString()
                                                    + ", SW2: " + responseApdu2.SW2.ToString()
                                                    + "\r\n"
                                                    + "Length: " + responseApdu2.Length.ToString() + "\r\n";
                            */

                            parse_tag(receiveBuffer);

                            // ③ここにデータ解析関数を実行

                            //textBox_Log.Text += "\r\n";

                        }

                    }

                }
            }
            textBox_Log.Text += "-----------------------------------------------\r\n";
        }

        private void parse_tag(byte[] data)
        {
            textBox_Log.Text += "履歴データ：" + BitConverter.ToString(data, 0, 18) + "\r\n";
        }

        private void button_ReadCardType_Click_1(object sender, EventArgs e)
        {
            var contextFactory = ContextFactory.Instance;

            using (var context = contextFactory.Establish(SCardScope.System))
            {
                var readerNames = context.GetReaders();
                if (NoReaderFound(readerNames))
                {
                    textBox_Log.Text += "You need at least one reader in order to run this example." + "\r\n";
                    return;
                }

                var readerName = ChooseRfidReader(readerNames);
                if (readerName == null)
                {
                    return;
                }

                // 'using' statement to make sure the reader will be disposed (disconnected) on exit
                using (var rfidReader = context.ConnectReader(readerName, SCardShareMode.Shared, SCardProtocol.Any))
                {
                    var apdu = new CommandApdu(IsoCase.Case2Short, rfidReader.Protocol)
                    {
                        CLA = 0xFF,
                        Instruction = InstructionCode.GetData,
                        P1 = 0xF3, // ここを変えています。
                        P2 = 0x00,
                        Le = 0 // We don't know the ID tag size
                    };

                    using (rfidReader.Transaction(SCardReaderDisposition.Leave))
                    {
                        textBox_Log.Text += "Retrieving the UID .... " + "\r\n";

                        var sendPci = SCardPCI.GetPci(rfidReader.Protocol);
                        var receivePci = new SCardPCI(); // IO returned protocol control information.

                        var receiveBuffer = new byte[256];
                        var command = apdu.ToArray();

                        var bytesReceived = rfidReader.Transmit(
                            sendPci, // Protocol Control Information (T0, T1 or Raw)
                            command, // command APDU
                            command.Length,
                            receivePci, // returning Protocol Control Information
                            receiveBuffer,
                            receiveBuffer.Length); // data buffer

                        var responseApdu =
                            new ResponseApdu(receiveBuffer, bytesReceived, IsoCase.Case2Short, rfidReader.Protocol);
                        textBox_Log.Text += "SW1: " + responseApdu.SW1.ToString()
                                                + ", SW2: " + responseApdu.SW2.ToString()
                                                + "\r\n";
                        if (responseApdu.HasData)
                        {
                            textBox_Log.Text += "Uid: " + BitConverter.ToString(responseApdu.GetData()) + "\r\n";
                        }
                        else
                        {
                            textBox_Log.Text += "Uid: No uid received" + "\r\n";
                        }

                    }
                }
            }
            textBox_Log.Text += "-----------------------------------------------\r\n";
        }
    }
}
