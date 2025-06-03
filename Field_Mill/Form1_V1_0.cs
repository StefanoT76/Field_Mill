
///////////////////////////////////////  Filed_Mill //////////////////////////////////////////////// 

using EasyModbus;
using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics.Eventing.Reader;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Net.NetworkInformation;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using static EasyModbus.ModbusClient;
using static ScottPlot.Generate;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Button;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.ToolBar;
using DateTime = System.DateTime;


namespace Field_Mill
{


    public partial class Form1 : Form
    {
        private Timer modbusPollTimer = new Timer();


        ModbusClient modbusClient;

        private string filePath;

        public Form1()
        {




            InitializeComponent();                   ////  Initialize serial communication

            filePath = Path.Combine(Application.StartupPath, "calibration.txt");



            radioButton15Min.CheckedChanged += RadioButtonTimeWindow_CheckedChanged;
            radioButton5Min.CheckedChanged += RadioButtonTimeWindow_CheckedChanged;
            radioButton60Min.CheckedChanged += RadioButtonTimeWindow_CheckedChanged;





            button8.Enabled = false;      /// Buttons used as indicators are disabled
            button10.Enabled = false;
            button11.Enabled = false;
            button12.Enabled = false;
            button13.Enabled = false;
            button14.Enabled = false;

            //// calibration buttons and texboxes are diabled until start is pressed 
            textBox1.Enabled = false;
            textBox2.Enabled = false;
            button27.Enabled = false;
            button28.Enabled = false;
            button24.Enabled = false;
            button25.Enabled = false;
        }


        //////////////////////////////////////  Load and save calibration to file ////////////////////////

        private void SaveResultToFile(float result)
        {
            try
            {
                File.WriteAllText(filePath, result.ToString(CultureInfo.InvariantCulture));
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error saving result: " + ex.Message);
            }
        }

        private void LoadResultFromFile()
        {
            try
            {
                if (File.Exists(filePath))
                {
                    string savedResult = File.ReadAllText(filePath);
                    if (float.TryParse(savedResult, NumberStyles.Float, CultureInfo.InvariantCulture, out float loaded))
                    {
                        label43.Text = loaded.ToString(CultureInfo.CurrentCulture);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading saved result: " + ex.Message);
            }
        }

        
        private double timeWindowMinutes = 15;
        

        private string csvFilePath;


        ////////////////////////////////   SERIAL PORT SELECTOR  ////////////////////////////////////////////////
        private void Form1_Load(object sender, EventArgs e)
        {
            comboBoxPollRate.Items.AddRange(new object[] { "100", "250", "500", "1000" }); // in milliseconds
            comboBoxPollRate.SelectedIndex = 3; // Default to 1000 ms

            modbusPollTimer.Tick += ModbusPollTimer_Tick;
            
            
            
            
            
            textBoxDate.Text = DateTime.Now.ToString("dd-MM-yyyy");
            textBoxDate.BackColor = Color.White;
            timer1.Interval = 60000; // check every minute
            timer1.Start();

            LoadResultFromFile();

            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            string logFolder = Path.Combine(Application.StartupPath, "Logs");
            Directory.CreateDirectory(logFolder);
            csvFilePath = Path.Combine(logFolder, $"log_{timestamp}.csv");

            File.AppendAllText(csvFilePath, "Timestamp,Reg0,Reg1_Float,Reg3_Float,Reg5_Float,Reg7_Long,Reg9_Binary,Reg10,Reg11,Reg12,Reg13_Float,Reg15_Float,Reg17_Float,Reg19\n");


            var ports = SerialPort.GetPortNames();
            comboBox1.DataSource = ports;
            SetupChart();


        }


        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (comboBox1.SelectedIndex > -1)
            {
                //MessageBox.Show(String.Format("You selected port '{0}'", comboBox1.SelectedItem));
                //Connect(comboBox1.SelectedItem.ToString());
                modbusClient = new ModbusClient(comboBox1.SelectedItem.ToString());
                modbusClient.UnitIdentifier = 1;
                modbusClient.Baudrate = 19200;
                modbusClient.Parity = System.IO.Ports.Parity.Even;
                modbusClient.StopBits = System.IO.Ports.StopBits.One;


            }
            else
            {
                MessageBox.Show("Please select a port first");
            }
        }

        private void button22_Click(object sender, EventArgs e)
        {
            try
            {
                if (modbusClient != null && !modbusClient.Connected)
                {
                    modbusClient.Connect();
                    labelStatus.Text = "Connected";
                    labelStatus.ForeColor = Color.Green;
                    //MessageBox.Show("Connected successfully.");
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                MessageBox.Show("Access denied to the port. It might be in use by another application.\n" + ex.Message);
            }
            catch (IOException ex)
            {
                MessageBox.Show("I/O error while connecting to port.\n" + ex.Message);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to connect to serial port:\n" + ex.Message);
            }
        }

        private void button23_Click(object sender, EventArgs e)
        {
            try
            {
                if (modbusClient != null && modbusClient.Connected)
                {
                    modbusClient.Disconnect();
                    labelStatus.Text = "Not Connected";
                    labelStatus.ForeColor = Color.Red;
                    //MessageBox.Show("Disconnected.");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error while disconnecting: " + ex.Message);
            }
        }

        ////////////////////////////  WRITE COILS ////////////////////////////////////////////

        private void Start_Click(object sender, EventArgs e)
        {
            modbusClient.WriteSingleCoil(4, true);

            bool[] Coil4 = new bool[1];

            Coil4 = modbusClient.ReadCoils(4, 1);
            if (Coil4[0] == false)
                label4.Text = "0";
            else
                label4.Text = "1";
        }

        private void Stop_Click(object sender, EventArgs e)
        {
            modbusClient.WriteSingleCoil(4, false);

            bool[] Coil4 = new bool[1];

            Coil4 = modbusClient.ReadCoils(4, 1);
            if (Coil4[0] == false)
                label4.Text = "0";
            else
                label4.Text = "1";
        }



        private void button2_Click(object sender, EventArgs e)
        {
            modbusClient.WriteSingleCoil(5, true);

            bool[] Coil5 = new bool[1];

            Coil5 = modbusClient.ReadCoils(5, 1);
            if (Coil5[0] == false)
                label5.Text = "0";
            else
                label5.Text = "1";
        }

        private void button1_Click(object sender, EventArgs e)
        {
            modbusClient.WriteSingleCoil(5, false);

            bool[] Coil5 = new bool[1];

            Coil5 = modbusClient.ReadCoils(5, 1);
            if (Coil5[0] == false)
                label5.Text = "0";
            else
                label5.Text = "1";
        }

        /////////////////////////////////////////////  ROTOR SPEED  //////////////////////////////

        private void button3_Click(object sender, EventArgs e)
        {
            int[] Reg0 = new int[1];

            try
            {
                if (modbusClient != null && modbusClient.Connected)
                {
                    // safe to use modbusClient
                    Reg0 = modbusClient.ReadHoldingRegisters(0, 1);
                    labelReg0.Text = Reg0[0].ToString();
                }
                else
                {
                    MessageBox.Show("Serial port is not connected.");
                }

                //Reg0 = modbusClient.ReadHoldingRegisters(0, 1);
                //label6.Text = Reg0[0].ToString();
            }
            catch (TimeoutException ex)
            {
                MessageBox.Show("No response from Modbus device.\nCheck wiring, address, and settings.\n\n" + ex.Message);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error during Modbus communication:\n" + ex.Message);
            }


        }

        ///////////////////////////////////////////  INSENSITVE CH CAL  ///////////////////////////

        private void button4_Click(object sender, EventArgs e)
        {
            float Reg1_2;

            Reg1_2 = EasyModbus.ModbusClient.ConvertRegistersToFloat(modbusClient.ReadHoldingRegisters(1, 2), RegisterOrder.LowHigh);
            labelReg1.Text = Reg1_2.ToString();
        }

        //////////////////////////////////////////  INSENSITIVE CH READING ///////////////////////

        private void button5_Click(object sender, EventArgs e)
        {
            float Reg3_4;

            Reg3_4 = EasyModbus.ModbusClient.ConvertRegistersToFloat(modbusClient.ReadHoldingRegisters(3, 2), RegisterOrder.LowHigh);
            labelReg3.Text = Reg3_4.ToString();
        }

        //////////////////////////////////////////  SENSITIVE CH READING ////////////////////////

        private void button6_Click(object sender, EventArgs e)
        {
            float Reg5_6;

            Reg5_6 = EasyModbus.ModbusClient.ConvertRegistersToFloat(modbusClient.ReadHoldingRegisters(5, 2), RegisterOrder.LowHigh);
            labelReg5.Text = Reg5_6.ToString();
        }

        ////////////////////////////////////////// INTENAL TEMPERATURE /////////////////////////////

        private void button7_Click(object sender, EventArgs e)
        {

            int[] Reg7_8 = new int[1];   /// This is a 32 bit long but here we get only the last 16 bits as otherwise became unstable
            Reg7_8 = modbusClient.ReadHoldingRegisters(8, 1);
            labelReg7.Text = Reg7_8[0].ToString();
        }

        /////////////////////////////////////////  STATUS ////////////////////////////////////////////////////////////////////
        private void button9_Click(object sender, EventArgs e)
        {

            int[] Reg9 = new int[1];
            Reg9 = modbusClient.ReadHoldingRegisters(9, 1);
            string s = Convert.ToString(Reg9[0], 2).PadLeft(16, '0');

            if (s[15] == '1')  // Plates short
            {
                button8.BackColor = Color.Red;
                button8.Text = "On";
            }
            else
            {
                button8.BackColor = Color.Green;
                button8.Text = "Off";
            }

            if (s[14] == '0')  // Motor ON
            {
                button10.BackColor = Color.Red;
                button10.Text = "Off";
            }
            else
            {
                button10.BackColor = Color.Green;
                button10.Text = "On";
            }

            if (s[13] == '0')  // Heater ON
            {
                button11.BackColor = Color.Green;
                button11.Text = "Off";
            }
            else
            {
                button11.BackColor = Color.Yellow;
                button11.Text = "On";
            }

            if (s[12] == '0')  // EEPROM WRITE
            {
                button12.BackColor = Color.Green;
                button12.Text = "Off";
            }
            else
            {
                button12.BackColor = Color.Blue;
                button12.Text = "WR";
            }

            if (s[7] == '0')  // Overflow InSensitive
            {
                button13.BackColor = Color.Green;
                button13.Text = "No";
            }
            else
            {
                button13.BackColor = Color.Orange;
                button13.Text = "Yes";
            }

            if (s[6] == '0')  // Overflow Sensitive
            {
                button14.BackColor = Color.Green;
                button14.Text = "No";
            }
            else
            {
                button14.BackColor = Color.Orange;
                button14.Text = "Yes";
            }

            labelReg9.Text = s;

        }

        ///////////////////////////// ZERO /////////////////////////////////////////////////////////
        private void button15_Click(object sender, EventArgs e)
        {
            int[] Reg10 = new int[1];

            Reg10 = modbusClient.ReadHoldingRegisters(10, 1);
            labelReg10.Text = Reg10[0].ToString();
        }

        private void button16_Click(object sender, EventArgs e)
        {
            int[] Reg11 = new int[1];

            Reg11 = modbusClient.ReadHoldingRegisters(11, 1);
            labelReg11.Text = Reg11[0].ToString();
        }

        /////////////////////////////////////// ERRORS ////////////////////////////////////////////

        private void button17_Click(object sender, EventArgs e)
        {
            int[] Reg12 = new int[1];

            Reg12 = modbusClient.ReadHoldingRegisters(12, 1);
            labelReg12.Text = Reg12[0].ToString();
        }

        ///////////////////////////////////// SENSITIVE CH CAL ////////////////////////////////////

        private void button18_Click(object sender, EventArgs e)
        {
            float Reg13_14;

            Reg13_14 = EasyModbus.ModbusClient.ConvertRegistersToFloat(modbusClient.ReadHoldingRegisters(13, 2), RegisterOrder.LowHigh);
            labelReg13.Text = Reg13_14.ToString();
        }

        ///////////////////////////////////  SENSITIVE CH OFFSET /////////////////////////////////////

        private void button19_Click(object sender, EventArgs e)
        {
            float Reg15_16;

            Reg15_16 = EasyModbus.ModbusClient.ConvertRegistersToFloat(modbusClient.ReadHoldingRegisters(15, 2), RegisterOrder.LowHigh);
            labelReg15.Text = Reg15_16.ToString();
        }

        ///////////////////////////////////  INSENSITIVE CH OFFSET /////////////////////////////////////

        private void button20_Click(object sender, EventArgs e)
        {
            float Reg17_18;

            Reg17_18 = EasyModbus.ModbusClient.ConvertRegistersToFloat(modbusClient.ReadHoldingRegisters(17, 2), RegisterOrder.LowHigh);
            labelReg17.Text = Reg17_18.ToString();
        }

        ///////////////////////////////////  SOFTWARE VERSION  ////////////////////////////////////////
        private void button21_Click(object sender, EventArgs e)
        {
            int[] Reg19 = new int[1];

            Reg19 = modbusClient.ReadHoldingRegisters(19, 1);
            labelReg19.Text = Reg19[0].ToString();
        }

        /////////////////////////////////// CALIBRATION FACTOR /////////////////////////////////////////

        private void button26_Click(object sender, EventArgs e)   ///  START
        {
            MessageBox.Show("Place the field mill at ground level facing up and insert the measurment in the Ground Level V/m box");
            textBox2.Enabled = true;

            button27.Enabled = true;

            textBox2.Focus();  // <-- Move focus here
        }

        private bool IsValidFloat(string input)  ///// Check Float function
        {
            // You can change CultureInfo to InvariantCulture if you want dot (.) as decimal separator
            return float.TryParse(input, NumberStyles.Float, CultureInfo.CurrentCulture, out _);
        }

        private void textBox2_KeyDown(object sender, KeyEventArgs e)  //// Check float in Ground level
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.Handled = true;
                e.SuppressKeyPress = true; // prevent ding sound or focus change



                if (IsValidFloat(textBox2.Text))
                {
                    MessageBox.Show("Place the field mill at final position level facing down and insert the measurment in the Final Position V/m box");
                    textBox1.Enabled = true;
                    button28.Enabled = true;
                    textBox1.Focus();  // <-- Move focus here
                }
                else
                    MessageBox.Show("Invalid input. Please enter a float number.");

            }
        }

        private void button27_Click(object sender, EventArgs e)   //// Read V/m from sensitive
        {
            float Reg5_6;

            Reg5_6 = EasyModbus.ModbusClient.ConvertRegistersToFloat(modbusClient.ReadHoldingRegisters(5, 2), RegisterOrder.LowHigh);
            textBox2.Text = Reg5_6.ToString();

            MessageBox.Show("Place the field mill at final position level facing down and insert the measurment in the Final Position V/m box");
            textBox1.Enabled = true;
            button28.Enabled = true;
            textBox1.Focus();  // <-- Move focus here
        }

        private void textBox1_KeyDown(object sender, KeyEventArgs e)  //// Check float in final position
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.Handled = true;
                e.SuppressKeyPress = true; // prevent ding sound or focus change



                if (IsValidFloat(textBox1.Text))
                {
                    MessageBox.Show("Now press the calculate button to obtain the correction factor");
                    button24.Enabled = true;
                    button24.Focus();  // <-- Move focus here
                }
                else
                    MessageBox.Show("Invalid input. Please enter a float number.");

            }
        }

        private void button28_Click(object sender, EventArgs e)  //// Read V/m from sensitive
        {
            float Reg5_6;

            Reg5_6 = EasyModbus.ModbusClient.ConvertRegistersToFloat(modbusClient.ReadHoldingRegisters(5, 2), RegisterOrder.LowHigh);
            textBox1.Text = Reg5_6.ToString();

            MessageBox.Show("Now press the calculate button to obtain the correction factor");
            button24.Enabled = true;
            button24.Focus();  // <-- Move focus here
        }

        private void button24_Click(object sender, EventArgs e)  /// Calculte the factor
        {
            if (float.TryParse(textBox2.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out float num1) &&
        float.TryParse(textBox1.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out float num2))
            {
                if (num2 == 0)
                {
                    MessageBox.Show("Cannot divide by zero.");
                    return;
                }

                float result = num1 / num2;
                label43.Text = result.ToString();

            }
            else
            {
                MessageBox.Show("Invalid input. Please enter valid float numbers in both boxes.");
            }


            MessageBox.Show("Now press the store button to save the correction factor");
            button25.Enabled = true;
            button25.Focus();  // <-- Move focus to store
        }

        private void button25_Click(object sender, EventArgs e)  //// store the value in a file
        {
            if (float.TryParse(label43.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out float num1))
            {

                string filePath = Path.Combine(Application.StartupPath, "caibration.txt");
                File.WriteAllText(filePath, num1.ToString(CultureInfo.InvariantCulture));

                SaveResultToFile(num1);
                textBox1.Enabled = false;
                textBox2.Enabled = false;
                button27.Enabled = false;
                button28.Enabled = false;
                button24.Enabled = false;
                button25.Enabled = false;

                MessageBox.Show("Calibration stored.");


            }

            else
            {
                MessageBox.Show("Invalid input. Please enter valid float numbers in both boxes.");
            }
        }

        ////////////////////////////////////////  POLLING   ///////////////////////////////////////////////
        private void buttonStartPolling_Click(object sender, EventArgs e)  /// START
        {
            if (!modbusClient.Connected)
                MessageBox.Show("Device is not connected.");

            // Set poll rate from ComboBox (default to 1000 ms if invalid)
            if (int.TryParse(comboBoxPollRate.SelectedItem?.ToString(), out int interval))
            {
                modbusPollTimer.Interval = interval;
            }
            else
            {
                modbusPollTimer.Interval = 1000;
            }

            modbusPollTimer.Start();
        }

        private void buttonStopPolling_Click(object sender, EventArgs e)    //// STOP
        {
            {
                modbusPollTimer.Stop();
            }
        }


        private void ModbusPollTimer_Tick(object sender, EventArgs e)
        {
            DateTime now = DateTime.Now;
            double timeX = now.ToOADate();

            try
            {
                if (modbusClient != null && modbusClient.Connected)
                {
                    int[] r = modbusClient.ReadHoldingRegisters(0, 20);

                    SetLabel(0, r[0].ToString()); // Int16

                    SetLabel(1, ConvertRegistersToFloat(r[1], r[2]).ToString("F2")); // Float
                    SetLabel(3, ConvertRegistersToFloat(r[3], r[4]).ToString("F2")); // Float
                    SetLabel(5, ConvertRegistersToFloat(r[5], r[6]).ToString("F2")); // Float

                    SetLabel(7, ConvertRegistersToLong(r[7], r[8]).ToString());      // Long


                    string s = Convert.ToString(r[9], 2).PadLeft(16, '0');   // Binary
                    SetLabel(9, s);  // Still update the label with the binary string

                    // Plates short
                    if (s[15] == '1')
                    {
                        button8.BackColor = Color.Red;
                        button8.Text = "On";
                    }
                    else
                    {
                        button8.BackColor = Color.Green;
                        button8.Text = "Off";
                    }

                    // Motor ON
                    if (s[14] == '0')
                    {
                        button10.BackColor = Color.Red;
                        button10.Text = "Off";
                    }
                    else
                    {
                        button10.BackColor = Color.Green;
                        button10.Text = "On";
                    }

                    // Heater ON
                    if (s[13] == '0')
                    {
                        button11.BackColor = Color.Green;
                        button11.Text = "Off";
                    }
                    else
                    {
                        button11.BackColor = Color.Yellow;
                        button11.Text = "On";
                    }

                    // EEPROM WRITE
                    if (s[12] == '0')
                    {
                        button12.BackColor = Color.Green;
                        button12.Text = "Off";
                    }
                    else
                    {
                        button12.BackColor = Color.Blue;
                        button12.Text = "WR";
                    }

                    // Overflow InSensitive
                    if (s[7] == '0')
                    {
                        button13.BackColor = Color.Green;
                        button13.Text = "No";
                    }
                    else
                    {
                        button13.BackColor = Color.Orange;
                        button13.Text = "Yes";
                    }

                    // Overflow Sensitive
                    if (s[6] == '0')
                    {
                        button14.BackColor = Color.Green;
                        button14.Text = "No";
                    }
                    else
                    {
                        button14.BackColor = Color.Orange;
                        button14.Text = "Yes";
                    }

                    //SetLabel(9, Convert.ToString(r[9], 2).PadLeft(16, '0'));         // Binary

                    
                    SetLabel(10, r[10].ToString()); // Int16
                    SetLabel(11, r[11].ToString());
                    SetLabel(12, r[12].ToString());

                    SetLabel(13, ConvertRegistersToFloat(r[13], r[14]).ToString("F2"));
                    SetLabel(15, ConvertRegistersToFloat(r[15], r[16]).ToString("F2"));
                    SetLabel(17, ConvertRegistersToFloat(r[17], r[18]).ToString("F2"));

                    SetLabel(19, r[19].ToString()); // Int16

                    // Parse float values from label text (safe version)
                    bool parsed3 = float.TryParse(labelReg3.Text.Split(':').Last().Trim(), out float value3);
                    bool parsed5 = float.TryParse(labelReg5.Text.Split(':').Last().Trim(), out float value5);

                    if (parsed3 && parsed5)
                    {
                        // Add points
                        chart1.Series["INS"].Points.AddXY(timeX, value3);
                        chart1.Series["SENS"].Points.AddXY(timeX, value5);

                        // Scroll X axis to always show last 15 or 5 minutes
                        double viewSize = TimeSpan.FromMinutes(timeWindowMinutes).TotalDays;
                        double minTime = timeX - viewSize;
                        
                        var axisX = chart1.ChartAreas["MainArea"].AxisX;
                        axisX.Minimum = minTime;
                        axisX.Maximum = timeX;
                        axisX.ScaleView.Zoom(minTime, timeX);

                       
                        
                    }
                    // Optional: remove old points for performance
                    // Define max age (61 minutes)
                    double maxAgeMinutes = 61;
                    double oldestAllowedTime = timeX - TimeSpan.FromMinutes(maxAgeMinutes).TotalDays;

                    // Clean up old points in Reg3
                    var series3 = chart1.Series["INS"];
                    while (series3.Points.Count > 0 && series3.Points[0].XValue < oldestAllowedTime)
                    {
                        series3.Points.RemoveAt(0);
                    }

                    // Clean up old points in Reg5
                    var series5 = chart1.Series["SENS"];
                    while (series5.Points.Count > 0 && series5.Points[0].XValue < oldestAllowedTime)
                    {
                        series5.Points.RemoveAt(0);
                    }




                    // Prepare a CSV row
                    string logLine = string.Join(",", new string[]
                    {
                        DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                        r[0].ToString(),
                        ConvertRegistersToFloat(r[1], r[2]).ToString("F2"),
                        ConvertRegistersToFloat(r[3], r[4]).ToString("F2"),
                        ConvertRegistersToFloat(r[5], r[6]).ToString("F2"),
                        ConvertRegistersToLong(r[7], r[8]).ToString(),
                        Convert.ToString(r[9], 2).PadLeft(16, '0'),
                        r[10].ToString(),
                        r[11].ToString(),
                        r[12].ToString(),
                        ConvertRegistersToFloat(r[13], r[14]).ToString("F2"),
                        ConvertRegistersToFloat(r[15], r[16]).ToString("F2"),
                        ConvertRegistersToFloat(r[17], r[18]).ToString("F2"),
                        r[19].ToString()
                    });

                    // Append to CSV
                    File.AppendAllText(csvFilePath, logLine + Environment.NewLine);


                }
                else
                {
                    ShowConnectionErrorInLabels("Not Connected");
                }
            }
            catch (TimeoutException)
            {
                ShowConnectionErrorInLabels("Timeout");
            }
            catch (Exception)
            {
                ShowConnectionErrorInLabels("Error");
            }
        }

        private float ConvertRegistersToFloat(int highWord, int lowWord)
        {
            byte[] bytes = new byte[4];
            bytes[0] = (byte)(lowWord >> 8);
            bytes[1] = (byte)(lowWord & 0xFF);
            bytes[2] = (byte)(highWord >> 8);
            bytes[3] = (byte)(highWord & 0xFF);
            return BitConverter.ToSingle(bytes.Reverse().ToArray(), 0); // Adjust endianness if needed
        }

        private long ConvertRegistersToLong(int highWord, int lowWord)
        {
            uint high = (uint)highWord;
            uint low = (uint)lowWord;
            return ((long)high << 16) | low;
        }


        private void SetLabel(int regIndex, string text)  /// Without register names
        {
            System.Windows.Forms.Label label = this.Controls.Find($"labelReg{regIndex}", true).FirstOrDefault() as System.Windows.Forms.Label;
            if (label != null)
            {
                label.Text = text; // Just the value, no prefix
            }
        }



       /* private void SetLabel(int regIndex, string text)   /// With register names
        {
            System.Windows.Forms.Label label = this.Controls.Find($"labelReg{regIndex}", true).FirstOrDefault() as System.Windows.Forms.Label;
            if (label != null)
            {
                label.Text = $"R{regIndex}: {text}";
            }
        }*/

        private void ShowConnectionErrorInLabels(string message)
        {
            for (int i = 0; i < 20; i++)
            {
                System.Windows.Forms.Label label = this.Controls.Find($"labelReg{i}", true).FirstOrDefault() as System.Windows.Forms.Label;
                if (label != null)
                {
                    label.Text = $"R{i}: {message}";
                }
            }
        }
        
        ////////////////////////////////////////  PLOTTING ////////////////////////////////////////////////
        
        private void SetupChart()
        {
            chart1.Series.Clear();
            chart1.ChartAreas.Clear();

            var chartArea = new ChartArea("MainArea");

            // Time-based X-axis
            
            
            chartArea.AxisX.LabelStyle.Format = "HH:mm:ss";
            chartArea.AxisX.Title = "Time";
            chartArea.AxisX.MajorGrid.LineColor = Color.LightGray;
            chartArea.AxisX.IntervalAutoMode = IntervalAutoMode.VariableCount;
            chartArea.AxisX.ScrollBar.Enabled = false;
            chartArea.AxisX.ScaleView.Zoomable = true;
            
       
            chartArea.AxisX.IntervalType = DateTimeIntervalType.Minutes;
            chartArea.AxisX.Interval = 1;

            // Set 15-minute view range (in OADate units)
            double minutes = 15;
            chartArea.AxisX.ScaleView.Size = TimeSpan.FromMinutes(minutes).TotalDays;

            // Y-axis auto-range
            chartArea.AxisY.Minimum = double.NaN;
            chartArea.AxisY.Maximum = double.NaN;
            chartArea.AxisY.Title = "V/m";

            chart1.ChartAreas.Add(chartArea);

            chart1.Series.Add(new Series("INS")
            {
                ChartType = SeriesChartType.Line,
                Color = Color.Blue,
                BorderWidth = 2,
                ChartArea = "MainArea"
            });

            chart1.Series.Add(new Series("SENS")
            {
                ChartType = SeriesChartType.Line,
                Color = Color.Red,
                BorderWidth = 2,
                ChartArea = "MainArea"
            });
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            textBoxDate.Text = DateTime.Now.ToString("dd-MM-yyyy");
        }

        private void buttonSaveForm_Click(object sender, EventArgs e)
        {
            using (SaveFileDialog saveFileDialog = new SaveFileDialog())
            {
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                saveFileDialog.Filter = "PNG Image|*.png|JPEG Image|*.jpg|Bitmap Image|*.bmp";
                saveFileDialog.Title = "Save Form As Image";
                saveFileDialog.FileName = $"form_{timestamp}.png";

                if (saveFileDialog.ShowDialog() == DialogResult.OK)
                {
                    Bitmap bmp = new Bitmap(this.Width, this.Height);
                    this.DrawToBitmap(bmp, new Rectangle(0, 0, this.Width, this.Height));

                    // Determine format
                    System.Drawing.Imaging.ImageFormat format = System.Drawing.Imaging.ImageFormat.Png;
                    string ext = Path.GetExtension(saveFileDialog.FileName).ToLower();
                    if (ext == ".jpg") format = System.Drawing.Imaging.ImageFormat.Jpeg;
                    else if (ext == ".bmp") format = System.Drawing.Imaging.ImageFormat.Bmp;

                    bmp.Save(saveFileDialog.FileName, format);
                    MessageBox.Show("Form saved as image successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }


        private void RadioButtonTimeWindow_CheckedChanged(object sender, EventArgs e)
        {
            if (radioButton5Min.Checked)
                timeWindowMinutes = 5;
            else if (radioButton15Min.Checked)
                timeWindowMinutes = 15;
            else if (radioButton60Min.Checked)
                timeWindowMinutes = 60;
        }
    }

}

