
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

            //filePath = Path.Combine(Application.StartupPath, "calibration.txt");

            //string filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "calibration.txt");
            string filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "My_Instruments", "Field_Mill", "calibration.txt");

            radioButton15Min.CheckedChanged += RadioButtonTimeWindow_CheckedChanged;
            radioButton5Min.CheckedChanged += RadioButtonTimeWindow_CheckedChanged;
            radioButton60Min.CheckedChanged += RadioButtonTimeWindow_CheckedChanged;





            //button8.Enabled = false;      /// Buttons used as indicators are disabled
            //button10.Enabled = false;
            //button11.Enabled = false;
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
                string folderPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "My_Instruments", "Field_Mill"
                );
                string filePath = Path.Combine(folderPath, "calibration.txt");

                // Ensure the directory exists
                Directory.CreateDirectory(folderPath);

                // Save using invariant culture
                File.WriteAllText(filePath, result.ToString(CultureInfo.InvariantCulture));
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error saving result: " + ex.Message, "File Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void LoadResultFromFile()
        {
            try
            {
                string filePath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "My_Instruments", "Field_Mill",
                    "calibration.txt"
                );

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
                MessageBox.Show("Error loading saved result: " + ex.Message, "File Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /* private void SaveResultToFile(float result)
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
         }*/


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

            LoadResultFromFile();    // load calibration data from file

            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");

            // Define the log folder path in Documents\My_Instruments\Field_Mill\Logs
            string logFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "My_Instruments", "Field_Mill", "Logs");

            // Ensure the directory exists
            Directory.CreateDirectory(logFolder);

            // Create full CSV file path
            csvFilePath = Path.Combine(logFolder, $"log_{timestamp}.csv");

            // Write CSV header
            File.AppendAllText(csvFilePath,
                "Timestamp,Rotor_Hz,INSENSITVE_CH_GAIN,INSENSITIVE_FIELD_V/m,SENSITIVE_FIELD_V/m,INTERNAL_TEMP,STATUS,INSENSITVE_CH_ZERO,SENSITVE_CH_ZERO,COM_ERRORS,SENSITIVE_CH_GAIN,SENSITIVE_CH_OFFSET,INSENSITIVE_CH_OFFSET,SW_VER\n");


            //string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            //string logFolder = Path.Combine(Application.StartupPath, "Logs");
            //Directory.CreateDirectory(logFolder);
            //csvFilePath = Path.Combine(logFolder, $"log_{timestamp}.csv");

            //File.AppendAllText(csvFilePath, "Timestamp,Rotor_Hz,INSENSITVE_CH_GAIN,INSENSITIVE_FIELD_V/m,SENSITIVE_FIELD_V/m,INTERNAL_TEMP,STATUS,INSENSITVE_CH_ZERO,SENSITVE_CH_ZERO,COM_ERRORS,SENSITIVE_CH_GAIN,SENSITIVE_CH_OFFSET,INSENSITIVE_CH_OFFSET,SW_VER\n");


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

       
        

        

       

    
        ///////////////////////////////////// SENSITIVE CH GAIN UPDATE////////////////////////////////////

        private void button18_Click(object sender, EventArgs e)
        {

            
            if (!float.TryParse(textBox3.Text, out float valueToWrite))
            {
                MessageBox.Show("Please enter a valid float number to send to Modbus register 13.", "Invalid Input", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                textBox3.Focus();
                textBox3.SelectAll();
                return;
            }

            try
            {
                
                modbusClient.WriteMultipleRegisters(13, EasyModbus.ModbusClient.ConvertFloatToRegisters((float)valueToWrite));
                MessageBox.Show("SENSITIVE CH GAIN successfully written to register 13.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to write to Modbus: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        ///////////////////////////////////  SENSITIVE CH OFFSET UPDATE /////////////////////////////////////

        private void button19_Click(object sender, EventArgs e)
        {
            if (!float.TryParse(textBox4.Text, out float valueToWrite))
            {
                MessageBox.Show("Please enter a valid float number to send to Modbus register 15.", "Invalid Input", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                textBox4.Focus();
                textBox4.SelectAll();
                return;
            }

            try
            {

                modbusClient.WriteMultipleRegisters(15, EasyModbus.ModbusClient.ConvertFloatToRegisters((float)valueToWrite));
                MessageBox.Show("SENSITIVE CH OFFSET successfully written to register 15.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to write to Modbus: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        ///////////////////////////////////////////  INSENSITVE CH GAIN UPDATE  ///////////////////////////

        private void button4_Click(object sender, EventArgs e)
        {
            if (!float.TryParse(textBox5.Text, out float valueToWrite))
            {
                MessageBox.Show("Please enter a valid float number to send to Modbus register 1.", "Invalid Input", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                textBox5.Focus();
                textBox5.SelectAll();
                return;
            }

            try
            {

                modbusClient.WriteMultipleRegisters(1, EasyModbus.ModbusClient.ConvertFloatToRegisters((float)valueToWrite));
                MessageBox.Show("INSENSITIVE CH GAIN successfully written to register 1.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to write to Modbus: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }




        ///////////////////////////////////  INSENSITIVE CH OFFSET UPDATE/////////////////////////////////////

        private void button20_Click(object sender, EventArgs e)
        {
            if (!float.TryParse(textBox6.Text, out float valueToWrite))
            {
                MessageBox.Show("Please enter a valid float number to send to Modbus register 17.", "Invalid Input", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                textBox6.Focus();
                textBox6.SelectAll();
                return;
            }

            try
            {

                modbusClient.WriteMultipleRegisters(17, EasyModbus.ModbusClient.ConvertFloatToRegisters((float)valueToWrite));
                MessageBox.Show("INSENSITIVE CH OFFSET successfully written to register 17.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to write to Modbus: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
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

       
        private void button27_Click(object sender, EventArgs e)   // Read V/m from sensitive
        {
            try
            {
                float reg5_6 = EasyModbus.ModbusClient.ConvertRegistersToFloat(
                    modbusClient.ReadHoldingRegisters(5, 2),
                    RegisterOrder.LowHigh
                );

                textBox2.Text = reg5_6.ToString("F2");

                MessageBox.Show("Place the field mill at final position level facing down and insert the measurement in the Final Position V/m box");

                textBox1.Enabled = true;
                button28.Enabled = true;
                textBox1.Focus();  // Move focus here
            }
            catch (TimeoutException)
            {
                MessageBox.Show("Timeout while reading registers 5 and 6. Check connection.", "Modbus Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error reading registers 5 and 6:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
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

       

        private void button28_Click(object sender, EventArgs e)  // Read V/m from sensitive
        {
            try
            {
                float reg5_6 = EasyModbus.ModbusClient.ConvertRegistersToFloat(
                    modbusClient.ReadHoldingRegisters(5, 2),
                    RegisterOrder.LowHigh
                );

                textBox1.Text = reg5_6.ToString("F2");

                MessageBox.Show("Now press the calculate button to obtain the correction factor");

                button24.Enabled = true;
                button24.Focus();  // Move focus to calculate button
            }
            catch (TimeoutException)
            {
                MessageBox.Show("Timeout while reading registers 5 and 6. Please check the connection.", "Modbus Timeout", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error reading registers 5 and 6:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
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


        private void button25_Click(object sender, EventArgs e)  // Store the value in a file
        {
            if (float.TryParse(label43.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out float num1))
            {
                string folderPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "My_Instruments", "Field_Mill"
                );
                string filePath = Path.Combine(folderPath, "calibration.txt");

                try
                {
                    // Ensure directory exists
                    Directory.CreateDirectory(folderPath);

                    // Write calibration value
                    File.WriteAllText(filePath, num1.ToString(CultureInfo.InvariantCulture));

                    // Optionally call SaveResultToFile(num1); if reusing that logic elsewhere

                    textBox1.Enabled = false;
                    textBox2.Enabled = false;
                    button27.Enabled = false;
                    button28.Enabled = false;
                    button24.Enabled = false;
                    button25.Enabled = false;

                    MessageBox.Show("Calibration stored in:\n" + filePath, "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error saving calibration file:\n" + ex.Message, "File Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            else
            {
                MessageBox.Show("Invalid input. Please enter a valid number in the calibration field.", "Input Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        /*private void button25_Click(object sender, EventArgs e)  //// store the value in a file
        {
            if (float.TryParse(label43.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out float num1))
            {
                
                string filePath = Path.Combine(Application.StartupPath, "calibration.txt");
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
        }*/

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

                    SetLabel(0, r[0].ToString()); // Int16      /// ROTOR SPEED

                    SetLabel(1, ConvertRegistersToFloat(r[1], r[2]).ToString("F2", CultureInfo.GetCultureInfo("it-IT"))); // Float  /// INSENS GAIN


                    float calibrationFactor = 1.0f;                                 /// display calibrated data in labels 
                    if (float.TryParse(label43.Text, out float parsedFactor))
                    {
                        calibrationFactor = parsedFactor;
                    }

                    float rawValue3 = ConvertRegistersToFloat(r[3], r[4]);
                    float rawValue5 = ConvertRegistersToFloat(r[5], r[6]);

                    float calibratedValue3 = rawValue3 * calibrationFactor;
                    float calibratedValue5 = rawValue5 * calibrationFactor;

                    SetLabel(3, calibratedValue3.ToString("F2"));
                    SetLabel(5, calibratedValue5.ToString("F2"));


                    //SetLabel(3, ConvertRegistersToFloat(r[3], r[4]).ToString("F2")); // Float
                    //SetLabel(5, ConvertRegistersToFloat(r[5], r[6]).ToString("F2")); // Float

                    SetLabel(7, ConvertRegistersToLong(r[7], r[8]).ToString());      // Long  // INTERNAL TEMP  


                    string s = Convert.ToString(r[9], 2).PadLeft(16, '0');   // Binary  STATUS
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
                        labelReg3.ForeColor = Color.LimeGreen;
                        button13.BackColor = Color.Green;
                        button13.Text = "No";
                    }
                    else
                    {
                        labelReg3.ForeColor = Color.Orange;
                        button13.BackColor = Color.Orange;
                        button13.Text = "Yes";
                    }

                    // Overflow Sensitive
                    if (s[6] == '0')
                    {
                        labelReg5.ForeColor = Color.LimeGreen;
                        button14.BackColor = Color.Green;
                        button14.Text = "No";
                    }
                    else
                    {
                        labelReg5.ForeColor = Color.Orange;
                        button14.BackColor = Color.Orange;
                        button14.Text = "Yes";
                    }

                    //SetLabel(9, Convert.ToString(r[9], 2).PadLeft(16, '0'));         // Binary

                    
                    SetLabel(10, r[10].ToString()); // Int16
                    SetLabel(11, r[11].ToString());
                    SetLabel(12, r[12].ToString());

                    
                    SetLabel(13, ConvertRegistersToFloat(r[13], r[14]).ToString("F2", CultureInfo.GetCultureInfo("it-IT")));    /// SENS GAIN
                    SetLabel(15, ConvertRegistersToFloat(r[15], r[16]).ToString("F2", CultureInfo.GetCultureInfo("it-IT")));    /// SENS OFFSET
                    SetLabel(17, ConvertRegistersToFloat(r[17], r[18]).ToString("F2", CultureInfo.GetCultureInfo("it-IT")));    /// INSENS OFFSET

                    SetLabel(19, r[19].ToString()); // Int16   SW VER

                    // Parse float values from label text (safe version)
                    bool parsed3 = float.TryParse(labelReg3.Text.Split(':').Last().Trim(), out float value3);
                    bool parsed5 = float.TryParse(labelReg5.Text.Split(':').Last().Trim(), out float value5);

                    if (parsed3 && parsed5)
                    {
                        // Add points
                        chart1.Series["INS"].Points.AddXY(timeX, value3);
                        chart1.Series["SENS"].Points.AddXY(timeX, value5);

                        // Scroll X axis to always show last 60 or 15 or 5 minutes
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
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                    r[0].ToString(CultureInfo.InvariantCulture),
                    ConvertRegistersToFloat(r[1], r[2]).ToString("F2", CultureInfo.InvariantCulture),
                    calibratedValue3.ToString("F2", CultureInfo.InvariantCulture),      // <- Calibrated value
                    calibratedValue5.ToString("F2", CultureInfo.InvariantCulture),      // <- Calibrated value
                    ConvertRegistersToLong(r[7], r[8]).ToString(CultureInfo.InvariantCulture),
                    Convert.ToString(r[9], 2).PadLeft(16, '0'),
                    r[10].ToString(CultureInfo.InvariantCulture),
                    r[11].ToString(CultureInfo.InvariantCulture),
                    r[12].ToString(CultureInfo.InvariantCulture),
                    ConvertRegistersToFloat(r[13], r[14]).ToString("F2", CultureInfo.InvariantCulture),
                    ConvertRegistersToFloat(r[15], r[16]).ToString("F2", CultureInfo.InvariantCulture),
                    ConvertRegistersToFloat(r[17], r[18]).ToString("F2", CultureInfo.InvariantCulture),
                    r[19].ToString(CultureInfo.InvariantCulture)
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



        

      /*  private void SetLabel(int regIndex, string text)
        {
            Control[] controls = this.Controls.Find($"labelReg{regIndex}", true);

            if (controls.Length > 0)
            {
                Control ctrl = controls[0];

                if (ctrl is System.Windows.Forms.Label label)
                {
                    label.Text = text;
                }
                else if (ctrl is System.Windows.Forms.TextBox textBox)
                {
                    textBox.Text = text;
                }
            }
        } */


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

        //////////////////////////////////////////////////// Image save /////////////////////////////////////////////////////////////////////

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

        //////////////////////////////////////////////////// Function Buttons /////////////////////////////////////////////////////////////////////

        private void button8_Click(object sender, EventArgs e)   // plate short enable or disable
        {
            try
            {
                // Step 1: Read current value of register 9
                int[] reg9 = modbusClient.ReadHoldingRegisters(9, 1);
                int currentValue = reg9[0];

                // Step 2: Toggle bit 0
                bool bit0IsSet = (currentValue & 0x0001) != 0;
                int newValue;

                if (bit0IsSet)
                {
                    // Clear bit 0
                    newValue = currentValue & ~0x0001;
                }
                else
                {
                    // Set bit 0
                    newValue = currentValue | 0x0001;
                }

                // Step 3: Write the modified value back to register 9
                modbusClient.WriteSingleRegister(9, newValue);

                // Optional: Feedback
               // MessageBox.Show($"Bit 0 has been {(bit0IsSet ? "cleared" : "set")}.", "Bit Toggle", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (TimeoutException)
            {
                MessageBox.Show("Timeout while communicating with Modbus device.", "Timeout", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error toggling bit 0 of register 9:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

       

        private void button10_Click(object sender, EventArgs e)  // rotor enable or disable
        {
            try
            {
                // Step 1: Read current value of register 9
                int[] reg9 = modbusClient.ReadHoldingRegisters(9, 1);
                int currentValue = reg9[0];

                // Step 2: Toggle bit 1 (0x0002 is the mask for bit 1)
                bool bit1IsSet = (currentValue & 0x0002) != 0;
                int newValue;

                if (bit1IsSet)
                {
                    // Clear bit 1
                    newValue = currentValue & ~0x0002;
                }
                else
                {
                    // Set bit 1
                    newValue = currentValue | 0x0002;
                }

                // Step 3: Write the modified value back to register 9
                modbusClient.WriteSingleRegister(9, newValue);

                // Optional: Visual feedback
                //MessageBox.Show($"Bit 1 has been {(bit1IsSet ? "cleared" : "set")}.", "Bit Toggle", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (TimeoutException)
            {
                MessageBox.Show("Timeout while communicating with Modbus device.", "Timeout", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error toggling bit 1 of register 9:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void button11_Click(object sender, EventArgs e)  // heater enable or disable
        {
            try
            {
                // Step 1: Read current value of register 9
                int[] reg9 = modbusClient.ReadHoldingRegisters(9, 1);
                int currentValue = reg9[0];

                // Step 2: Toggle bit 2 (0x0004 is the mask for bit 2)
                bool bit2IsSet = (currentValue & 0x0004) != 0;
                int newValue;

                if (bit2IsSet)
                {
                    // Clear bit 2
                    newValue = currentValue & ~0x0004;
                }
                else
                {
                    // Set bit 2
                    newValue = currentValue | 0x0004;
                }

                // Step 3: Write the modified value back to register 9
                modbusClient.WriteSingleRegister(9, newValue);

                // Optional: Visual feedback
                //MessageBox.Show($"Bit 2 has been {(bit2IsSet ? "cleared" : "set")}.", "Bit Toggle", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (TimeoutException)
            {
                MessageBox.Show("Timeout while communicating with Modbus device.", "Timeout", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error toggling bit 2 of register 9:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void button1_Click(object sender, EventArgs e) // write EEPROM
        {
            try
            {
                // 0x000A = 10 decimal
                int pattern = 0x000A;

                modbusClient.WriteSingleRegister(9, pattern);

                MessageBox.Show("Calibration data written to EEPROM successfully, wait for reboot.",
                                "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (TimeoutException)
            {
                MessageBox.Show("Operation timed out while writing to register 9.",
                                "Timeout", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to write to register 9: " + ex.Message,
                                "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

}

