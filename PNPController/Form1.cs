using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Threading;     // DLL support
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Forms.RibbonHelpers;


namespace PNPController
{
    public partial class Form1 : RibbonForm
    {
        // bed settings
        public double NeedleZHeight = -36.7;
        public double NeedleXSpacing = -32.2;
        public double dblPCBThickness = 1.6;
        public int FeedRate = 20000;
        public int chipfeederms = 250;

        public DataSet dsMain;
        public DataTable dtFeeders;
        // Add these two lines
        private Mach4.IMach4 Mach = null;
        private Mach4.IMyScriptObject Script = null;

       


        // manual picker selector
        public int currentfeeder = 0;

        private delegate void SetTextCallback(System.Windows.Forms.Control control, string text);

        private delegate void SetDROTextCallback(System.Windows.Forms.RibbonTextBox control, string text);


        private ComponentFeeders cf = new ComponentFeeders();
        private Components comp = new Components();
        private PCBLoader csvload = new PCBLoader();
        private DataToMach3 dtom3 = new DataToMach3();

        // define mach3 output short mapping
        short OUTPUT1 = 7;      //  arduino 1
        short OUTPUT2 = 8;       //  arduino 2
        short OUTPUT3 = 9;       //  arduino 3
        short OUTPUT4 = 10;      //  arduino 4
        short OUTPUT5 = 11;      //  arduino 5
        // short OUTPUT6 = 12;
        short OUTPUT7 = 16;      //  arduino interupt
        short OUTPUT8 = 17;     //  Vacuum Picker 1
        short OUTPUT9 = 18;     //  Vacuum Picker 2
        short OUTPUT10 = 19;    //  Relay 1
        short OUTPUT11 = 20;    //  Relay 2
        short OUTPUT12 = 21;    //  Relay 3
        short OUTPUT13 = 22;    //  Relay 4
        short OUTPUT14 = 23;    //  Relay 5
        short OUTPUT15 = 24;    //  Relay 6
        short OUTPUT16 = 25;    //  Relay 7
        short OUTPUT17 = 26;    //  Relay 8
        // short OUTPUT18 = 27;
        //short OUTPUT19 = 28;
        //short OUTPUT20 = 29;
        short INPUT1 = 18;


        public Form1()
        {
            InitializeComponent();

            Mach = (Mach4.IMach4)Marshal.GetActiveObject("Mach4.Document");
            Script = (Mach4.IMyScriptObject)Mach.GetScriptDispatch();

            InitializeBackgroundWorker();
            this.FormClosing += new FormClosingEventHandler(Form_FormClosing);

            dtFeeders = cf.POPFeedersTable();

            LoadSettings();
            /*
            comboBoxManualReelFeeder.DisplayMember = "feederNumber";
            comboBoxManualReelFeeder.ValueMember = "feederNumber";
            comboBoxManualReelFeeder.DataSource = dtFeeders;
            comboBoxManualReelFeeder.SelectedIndex = 0;
            */
            csvload.SetupGridView(dataGridView1);


            // setup manual feeder single picker list

            
            
        }

        void Form_FormClosing(object sender, FormClosingEventArgs e)
        {
            SaveSettings();
            timer1.Stop();
            backgroundWorkerDoCommand.CancelAsync();
            backgroundWorkerUpdateDRO.CancelAsync();
          }

        public double CalcAZHeight(double targetheight)
        {
            return NeedleZHeight + targetheight;
        }
        public double CalcAZPlaceHeight(double targetheight)
        {
            return (NeedleZHeight + targetheight) + dblPCBThickness;
        }

        public double CalcXwithNeedleSpacing(double target)
        {
            return (NeedleXSpacing + target);
        }


       // Thread safe updating of control's text property
        public void SetText(System.Windows.Forms.Control control, string text)
        {
            try
            {
                if (control.InvokeRequired)
                {

                    SetTextCallback d = new SetTextCallback(SetText);
                    Invoke(d, new object[] { control, text });

                }
                else
                {
                    control.Text = text;
                }
            }
            catch { }
        }
        
        public void SetDROText(System.Windows.Forms.RibbonTextBox control, string text)
        {
            try
            {
                if (this.ribbon1.InvokeRequired)
                {

                    SetDROTextCallback d = new SetDROTextCallback(SetDROText);
                    Invoke(d, new object[] { control, text });

                }
                else
                {
                    control.TextBoxText = text;
                }
            }
            catch { }
        }
        
        private void StetActiveComponentText(string text)
        {
            text = "Current Component: " + text;
            if (this.statusStrip1.InvokeRequired)
                this.statusStrip1.Invoke(
                                       new MethodInvoker(() => this.toolStripStatusLabelCurrentCommand.Text = text));
            else this.toolStripStatusLabelCurrentCommand.Text = text;
        }

        private void SetCurrentCommandText(string text)
        {
            text = "Active Command: " + text;
            if (this.statusStrip1.InvokeRequired)
                this.statusStrip1.Invoke(
                                       new MethodInvoker(() => this.toolStripStatusLabelActiveComponent.Text = text));
            else this.toolStripStatusLabelActiveComponent.Text = text;
        }

        private void SetResultsLabelText(string text)
        {
            if (this.statusStrip1.InvokeRequired)
                this.statusStrip1.Invoke(
                                       new MethodInvoker(() => this.toolStripStatusLabelresultLabel.Text = text));
            else this.toolStripStatusLabelresultLabel.Text = text;
        } 

        


        private void InitializeBackgroundWorker()
        {
            backgroundWorkerUpdateDRO.DoWork +=
                new DoWorkEventHandler(backgroundWorkerUpdateDRO_DoWork_1);
            backgroundWorkerUpdateDRO.RunWorkerCompleted +=
                new RunWorkerCompletedEventHandler(
            backgroundWorkerUpdateDRO_RunWorkerCompleted);
            backgroundWorkerUpdateDRO.ProgressChanged +=
                new ProgressChangedEventHandler(
            backgroundWorkerUpdateDRO_ProgressChanged);




            backgroundWorkerDoCommand.DoWork +=
                new DoWorkEventHandler(backgroundWorkerDoCommand_DoWork);
            backgroundWorkerDoCommand.RunWorkerCompleted +=
                new RunWorkerCompletedEventHandler(
            backgroundWorkerDoCommand_RunWorkerCompleted);
            backgroundWorkerDoCommand.ProgressChanged +=
                new ProgressChangedEventHandler(
            backgroundWorkerDoCommand_ProgressChanged);



            backgroundWorkerGetFeeder.DoWork +=
                new DoWorkEventHandler(backgroundWorkerGetFeeder_DoWork);
            backgroundWorkerGetFeeder.RunWorkerCompleted +=
                new RunWorkerCompletedEventHandler(
            backgroundWorkerGetFeeder_RunWorkerCompleted);
            backgroundWorkerGetFeeder.ProgressChanged +=
                new ProgressChangedEventHandler(
            backgroundWorkerGetFeeder_ProgressChanged);




            backgroundWorkerChipFeeder.DoWork +=
                new DoWorkEventHandler(backgroundWorkerChipFeeder_DoWork);
        }






        private void backgroundWorkerUpdateDRO_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            toolStripStatusLabelresultLabel.Text = "Running!";
        }
        private void backgroundWorkerDoCommand_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            toolStripProgressBar1.Value = e.ProgressPercentage;
            toolStripStatusLabelresultLabel.Text = "Running!";
        }
        private void backgroundWorkerGetFeeder_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            toolStripStatusLabelresultLabel.Text = "Running!";
        }

        // This event handler deals with the results of the background operation. 
        private void backgroundWorkerUpdateDRO_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Cancelled == true)
            {
                toolStripStatusLabelresultLabel.Text = "Cancelled!";
            }
            else if (e.Error != null)
            {
                toolStripStatusLabelresultLabel.Text = "Error: " + e.Error.Message;
            }
            else
            {
                toolStripStatusLabelresultLabel.Text = "Done!";
            }
        }
        private void backgroundWorkerDoCommand_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Cancelled == true)
            {
                toolStripStatusLabelresultLabel.Text = "Canceled!";
            }
            else if (e.Error != null)
            {
                toolStripStatusLabelresultLabel.Text = "Error: " + e.Error.Message;
            }
            else
            {
                toolStripStatusLabelresultLabel.Text = "Done!";
            }
        }

        private void backgroundWorkerGetFeeder_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Cancelled == true)
            {
                toolStripStatusLabelresultLabel.Text = "Canceled!";
            }
            else if (e.Error != null)
            {
                toolStripStatusLabelresultLabel.Text = "Error: " + e.Error.Message;
            }
            else
            {
                toolStripStatusLabelresultLabel.Text = "Done!";
            }
        }

        private void CheckFeederReady(Mach4.IMyScriptObject script)
        {


            while (script.IsActive(INPUT1) != 0)
            {
                Thread.Sleep(50);
            }
        }

        private void backgroundWorkerGetFeeder_DoWork(object sender, DoWorkEventArgs e)
        {
            // get current position, move to feeder, pick component and return to previous position
            BackgroundWorker worker = sender as BackgroundWorker;

            double currentx = Double.Parse(Script.GetParam("XDRO").ToString());
            double currenty = Double.Parse(Script.GetParam("YDRO").ToString());


            SetFeederOutputs(currentfeeder);
            double feederPosX = cf.GetfeederPosX(currentfeeder.ToString());
            double feederPosY = cf.GetfeederPosY(currentfeeder.ToString());
            double feederPosZ = cf.GetfeederPosZ(currentfeeder.ToString());




            RunMach3Command(Script, "F" + ribbonTextBoxFeedRate.TextBoxText);
            ChangeVacOutput(1, false);
            RunMach3Command(Script, "G01 A" + CalcAZHeight(20));
            RunMach3Command(Script, "G01 X" + feederPosX.ToString() + "  Y" + feederPosY);
            CheckFeederReady(Script);
            RunMach3Command(Script, "G01 A" + CalcAZHeight(feederPosZ)); // go down and turn on suction
            ChangeVacOutput(1, true);
            RunMach3Command(Script, "G01 A" + CalcAZHeight(20));
            RunMach3Command(Script, "G01 X" + currentx.ToString() + "  Y" + currenty.ToString());

            //worker.ReportProgress(1);
        }

        private void backgroundWorkerUpdateDRO_DoWork_1(object sender, DoWorkEventArgs e)
        {
            BackgroundWorker worker = sender as BackgroundWorker;


            if (backgroundWorkerUpdateDRO.CancellationPending)
            {
                e.Cancel = true;
            }
            SetDROText(ribbonTextBoxDROX, Script.GetParam("XDRO").ToString());
            SetDROText(ribbonTextBoxDROY, Script.GetParam("YDRO").ToString());
            SetDROText(ribbonTextBoxDROZ, Script.GetParam("ZDRO").ToString());
            SetDROText(ribbonTextBoxDROA, Script.GetParam("ADRO").ToString());
            SetDROText(ribbonTextBoxDROB, Script.GetParam("BDRO").ToString());
            SetDROText(ribbonTextBoxDROC, Script.GetParam("CDRO").ToString());
            worker.ReportProgress(100);
        }
        private void backgroundWorkerDoCommand_DoWork(object sender, DoWorkEventArgs e)
        {
            BackgroundWorker worker = sender as BackgroundWorker;
            DataView rundata = new DataView();
            rundata = dtom3.ConvertToGCode(dataGridView1, Double.Parse(ribbonTextBoxBoardOffsetX.TextBoxText), Double.Parse(ribbonTextBoxBoardOffsetY.TextBoxText));

            RunMach3Command(Script, "F" + FeedRate.ToString());
            int currentrow = 0;
            int totalrows = rundata.Count;
           // MessageBox.Show(totalrows.ToString());
            RunMach3Command(Script, "G01 B0 C0");

            /* table columns
             RefDes
                Type
                PosX
                PosY
                ComponentRotation
                ComponentValue
                feederNumber
                Code


                ComponentHeight
                DefaultRotation
                VerifywithCamera
                TapeFeeder


                feederPosX
                feederPosY
                feederPosZ
                PickPlusChipHeight
             * */
            
            double progresspert = 0;
            double progresspertcalc = 100/ totalrows;
            while (currentrow < totalrows)
            {

                if (backgroundWorkerDoCommand.CancellationPending)
                {
                    e.Cancel = true;
                    SetCurrentCommandText("Stopped");

                   
                    rundata.Dispose();
                    break;
                }

                if (currentrow == 0)
                {
                    SetFeederOutputs(Int32.Parse(rundata[currentrow]["FeederNumber"].ToString())); // send feeder to position
                }
                SetCurrentCommandText("Feeder Activate");
                SetCurrentCommandText(rundata[currentrow]["RefDes"].ToString());
                StetActiveComponentText(rundata[currentrow]["RefDes"].ToString());
                RunMach3Command(Script, "G01 X" + rundata[currentrow]["feederPosX"].ToString() + "  Y" + rundata[currentrow]["feederPosY"].ToString());

                SetCurrentCommandText("Picking Component");
                if (rundata[currentrow]["TapeFeeder"].ToString().Equals("True"))
                {
                    // use picker 1
                    RunMach3Command(Script, "G01 A" + CalcAZHeight(Double.Parse(rundata[currentrow]["feederPosZ"].ToString()))); // go down and turn on suction
                    ChangeVacOutput(1, true);
                    RunMach3Command(Script, "G01 A" + CalcAZHeight(20));


                }
                else
                {
                    // use picker 2


                    RunMach3Command(Script, "G01 Z" + CalcAZHeight(Double.Parse(rundata[currentrow]["feederPosZ"].ToString()))); // go down and turn on suction
                    ChangeVacOutput(2, true);
                    //Thread.Sleep(500);
                    RunMach3Command(Script, "G01 Z" + CalcAZHeight(20));
                }
                // send picker to pick next item
                if (currentrow >= 0 && (currentrow + 1) < totalrows)
                {
                    SetFeederOutputs(Int32.Parse(rundata[currentrow + 1]["FeederNumber"].ToString())); // send feeder to position
                }

                // rotate head
                double ComponentRotation = Double.Parse(rundata[currentrow]["ComponentRotation"].ToString());
                double DefaultRotation = Double.Parse(rundata[currentrow]["DefaultRotation"].ToString());

                
                
                RunMach3Command(Script, "G92 B0 C0 ");
                SetResultsLabelText("Placing Component");
                if (rundata[currentrow]["TapeFeeder"].ToString().Equals("True"))
                {
                    // use picker 1
                    if (DefaultRotation != ComponentRotation)
                    {
                        double newrotation = DefaultRotation + ComponentRotation;
                        RunMach3Command(Script, "G01 B" + newrotation.ToString() + " X" + rundata[currentrow]["PosX"].ToString() + "  Y" + rundata[currentrow]["PosY"].ToString());
                    }
                    else
                    {
                        RunMach3Command(Script, "G01 X" + rundata[currentrow]["PosX"].ToString() + "  Y" + rundata[currentrow]["PosY"].ToString());
                    }
                    RunMach3Command(Script, "G01 A" + CalcAZPlaceHeight(Double.Parse(rundata[currentrow]["ComponentHeight"].ToString()))); // go down and turn off suction
                    ChangeVacOutput(1, false);
                    RunMach3Command(Script, "G01 A" + CalcAZHeight(20));
                }
                else
                {
                    // use picker 2  CalcXwithNeedleSpacing
                    if (DefaultRotation != ComponentRotation)
                    {
                        double newrotation = DefaultRotation + ComponentRotation;
                        RunMach3Command(Script, "G01 C" + newrotation.ToString() + " X" + CalcXwithNeedleSpacing(Double.Parse(rundata[currentrow]["PosX"].ToString())).ToString() + "  Y" + rundata[currentrow]["PosY"].ToString());
                    }
                    else
                    {
                        RunMach3Command(Script, "G01 X" + CalcXwithNeedleSpacing(Double.Parse(rundata[currentrow]["PosX"].ToString())).ToString() + "  Y" + rundata[currentrow]["PosY"].ToString());
                    }
                    RunMach3Command(Script, "G01 Z" + CalcAZPlaceHeight(Double.Parse(rundata[currentrow]["ComponentHeight"].ToString()))); // go down and turn off suction
                    ChangeVacOutput(2, false);
                    RunMach3Command(Script, "G01 Z" + CalcAZHeight(20));
                }







                currentrow++;
                progresspert = currentrow * progresspertcalc;
                worker.ReportProgress(Int32.Parse(progresspert.ToString()));
            }
            rundata.Dispose();
            // home all axis and zero
            SetResultsLabelText("Home and Reset");
            RunMach3Command(Script, "G92 B0 C0 ");
            backgroundWorkerDoCommand.CancelAsync();
          
            worker.ReportProgress(100);
        }

        public bool RunMach3Command(Mach4.IMyScriptObject script, string command)
        {
            SetCurrentCommandText(command);
            script.Code(command);
            try
            {
                while (script.IsMoving() != 0)
                {
                    Thread.Sleep(10);
                }
            }
            catch { }
            return true;
        }

        public void SetRelayStatus(int relay, bool turnon)
        {
            short RelaytoUpdate = 0;
            switch (relay)
            {
                case 1:
                    RelaytoUpdate = OUTPUT10;
                    break;
                case 2:
                    RelaytoUpdate = OUTPUT11;
                    break;
                case 3:
                    RelaytoUpdate = OUTPUT12;
                    break;
                case 4:
                    RelaytoUpdate = OUTPUT13;
                    break;
                case 5:
                    RelaytoUpdate = OUTPUT14;
                    break;
                case 6:
                    RelaytoUpdate = OUTPUT15;
                    break;
                case 7:
                    RelaytoUpdate = OUTPUT16;
                    break;
                case 8:
                    RelaytoUpdate = OUTPUT17;
                    break;
            }
            if (turnon)
            {
                Script.ActivateSignal(RelaytoUpdate);
            }
            else
            {
                Script.DeActivateSignal(RelaytoUpdate);
            }
        }

        public void ChangeVacOutput(int vac, bool turnon)
        {
            //   Mach = (Mach4.IMach4)Marshal.GetActiveObject("Mach4.Document");
            //   Script = (Mach4.IMyScriptObject)Mach.GetScriptDispatch();
            if (vac == 1)
            {
                if (turnon)
                {
                    Script.ActivateSignal(OUTPUT8);
                    Thread.Sleep(100);
                }
                else
                {
                    Script.DeActivateSignal(OUTPUT8);
                }

            }
            else
            {
                if (turnon)
                {
                    Script.ActivateSignal(OUTPUT9);
                    Thread.Sleep(500);
                }
                else
                {
                    Script.DeActivateSignal(OUTPUT9);
                    Thread.Sleep(1000);
                }
            }
            // Thread.Sleep(50);


        }
        public void SetFeederOutputs(int feedercommand)
        {
            //  Mach = (Mach4.IMach4)Marshal.GetActiveObject("Mach4.Document");
            //   Script = (Mach4.IMyScriptObject)Mach.GetScriptDispatch();


            switch (feedercommand)
            {
                case 0:

                    break;
                case 1:
                    Script.DeActivateSignal(OUTPUT1);
                    Script.DeActivateSignal(OUTPUT2);
                    Script.DeActivateSignal(OUTPUT3);
                    Script.DeActivateSignal(OUTPUT4);
                    Script.DeActivateSignal(OUTPUT7);
                    Script.ActivateSignal(OUTPUT5);
                    break;
                case 2:
                    Script.DeActivateSignal(OUTPUT1);
                    Script.DeActivateSignal(OUTPUT2);
                    Script.DeActivateSignal(OUTPUT3);
                    Script.DeActivateSignal(OUTPUT5);
                    Script.DeActivateSignal(OUTPUT7);
                    Script.ActivateSignal(OUTPUT4);
                    break;
                case 3:
                    Script.DeActivateSignal(OUTPUT1);
                    Script.DeActivateSignal(OUTPUT2);
                    Script.DeActivateSignal(OUTPUT3);
                    Script.DeActivateSignal(OUTPUT7);
                    Script.ActivateSignal(OUTPUT4);
                    Script.ActivateSignal(OUTPUT5);
                    break;
                case 4:
                    Script.DeActivateSignal(OUTPUT1);
                    Script.DeActivateSignal(OUTPUT2);
                    Script.DeActivateSignal(OUTPUT4);
                    Script.DeActivateSignal(OUTPUT5);
                    Script.DeActivateSignal(OUTPUT7);
                    Script.ActivateSignal(OUTPUT3);
                    break;
                case 5:
                    Script.DeActivateSignal(OUTPUT1);
                    Script.DeActivateSignal(OUTPUT2);
                    Script.DeActivateSignal(OUTPUT4);
                    Script.DeActivateSignal(OUTPUT7);
                    Script.ActivateSignal(OUTPUT3);
                    Script.ActivateSignal(OUTPUT5);
                    break;
                case 6:
                    Script.DeActivateSignal(OUTPUT1);
                    Script.DeActivateSignal(OUTPUT2);
                    Script.DeActivateSignal(OUTPUT5);
                    Script.DeActivateSignal(OUTPUT7);
                    Script.ActivateSignal(OUTPUT3);
                    Script.ActivateSignal(OUTPUT4);
                    break;
                case 7:
                    Script.DeActivateSignal(OUTPUT1);
                    Script.DeActivateSignal(OUTPUT2);
                    Script.DeActivateSignal(OUTPUT7);
                    Script.ActivateSignal(OUTPUT3);
                    Script.ActivateSignal(OUTPUT4);
                    Script.ActivateSignal(OUTPUT5);
                    break;
                case 8:
                    Script.DeActivateSignal(OUTPUT1);
                    Script.DeActivateSignal(OUTPUT3);
                    Script.DeActivateSignal(OUTPUT4);
                    Script.DeActivateSignal(OUTPUT5);
                    Script.DeActivateSignal(OUTPUT7);
                    Script.ActivateSignal(OUTPUT2);
                    break;
                case 9:
                    Script.DeActivateSignal(OUTPUT1);
                    Script.DeActivateSignal(OUTPUT3);
                    Script.DeActivateSignal(OUTPUT4);
                    Script.DeActivateSignal(OUTPUT7);
                    Script.ActivateSignal(OUTPUT2);
                    Script.ActivateSignal(OUTPUT5);
                    break;
                case 10:
                    Script.DeActivateSignal(OUTPUT1);
                    Script.DeActivateSignal(OUTPUT3);
                    Script.DeActivateSignal(OUTPUT5);
                    Script.DeActivateSignal(OUTPUT7);
                    Script.ActivateSignal(OUTPUT2);
                    Script.ActivateSignal(OUTPUT4);
                    break;
                case 11:
                    Script.DeActivateSignal(OUTPUT1);
                    Script.DeActivateSignal(OUTPUT3);
                    Script.DeActivateSignal(OUTPUT7);
                    Script.ActivateSignal(OUTPUT2);
                    Script.ActivateSignal(OUTPUT4);
                    Script.ActivateSignal(OUTPUT5);
                    break;
                case 12:
                    Script.DeActivateSignal(OUTPUT1);
                    Script.DeActivateSignal(OUTPUT4);
                    Script.DeActivateSignal(OUTPUT5);
                    Script.DeActivateSignal(OUTPUT7);
                    Script.ActivateSignal(OUTPUT2);
                    Script.ActivateSignal(OUTPUT3);
                    break;
                case 13:
                    Script.DeActivateSignal(OUTPUT1);
                    Script.DeActivateSignal(OUTPUT4);
                    Script.DeActivateSignal(OUTPUT7);
                    Script.ActivateSignal(OUTPUT2);
                    Script.ActivateSignal(OUTPUT3);
                    Script.ActivateSignal(OUTPUT5);
                    break;
                case 14:
                    Script.DeActivateSignal(OUTPUT1);
                    Script.DeActivateSignal(OUTPUT5);
                    Script.DeActivateSignal(OUTPUT7);
                    Script.ActivateSignal(OUTPUT2);
                    Script.ActivateSignal(OUTPUT3);
                    Script.ActivateSignal(OUTPUT4);
                    break;
                case 15:
                    Script.DeActivateSignal(OUTPUT1);
                    Script.DeActivateSignal(OUTPUT7);
                    Script.ActivateSignal(OUTPUT2);
                    Script.ActivateSignal(OUTPUT3);
                    Script.ActivateSignal(OUTPUT4);
                    Script.ActivateSignal(OUTPUT5);
                    break;
                case 98: // reset feeders
                    Script.DeActivateSignal(OUTPUT7);
                    Script.ActivateSignal(OUTPUT1);
                    Script.ActivateSignal(OUTPUT2);
                    Script.ActivateSignal(OUTPUT3);
                    Script.ActivateSignal(OUTPUT4);
                    Script.ActivateSignal(OUTPUT5);
                    break;
            }
            // check if on main feeder rack
            if (feedercommand < 20 || feedercommand == 98)
            {
                // command set, now toggle interupt pin
                Script.ActivateSignal(OUTPUT7);
                Thread.Sleep(125);
                Script.DeActivateSignal(OUTPUT7);
            }

            if (feedercommand > 20 && feedercommand < 30)
            {
                chipfeederms = Int32.Parse(ribbonTextBoxChipMS.TextBoxText);
                if (backgroundWorkerChipFeeder.IsBusy != true)
                {
                    backgroundWorkerChipFeeder.RunWorkerAsync();
                }
            }
        }
        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }
        // update the DRO screen with current position
        private void timer1_Tick(object sender, EventArgs e)
        {
            if (backgroundWorkerUpdateDRO.IsBusy != true)
            {
                // Start the asynchronous operation.
                backgroundWorkerUpdateDRO.RunWorkerAsync();
            }
           
        }


        private void Form1_Load(object sender, EventArgs e)
        {



            // LoadCamera(pictureBox1, "Video Camera", 4, cam, true);
            // LoadCamera(pictureBox2, "USB Camera", 4, cam2, false);
        }

        private void buttonStopGCode_Click(object sender, EventArgs e)
        {
            backgroundWorkerDoCommand.CancelAsync();


        }

        private void buttonChipFeederGo_Click(object sender, EventArgs e)
        {
            toolStripProgressBar1.Value = 0;
           // currentfeeder = Int32.Parse(comboBoxManualReelFeeder.SelectedValue.ToString());
            if (backgroundWorkerGetFeeder.IsBusy != true)
            {
                backgroundWorkerGetFeeder.RunWorkerAsync();
            }
        }

       

       

       

        private void buttonChipFeeder_Click(object sender, EventArgs e)
        {
            chipfeederms = Int32.Parse(ribbonTextBoxChipMS.TextBoxText);
            if (backgroundWorkerChipFeeder.IsBusy != true)
            {
                backgroundWorkerChipFeeder.RunWorkerAsync();
            }
        }

        private void backgroundWorkerChipFeeder_DoWork(object sender, DoWorkEventArgs e)
        {
            SetRelayStatus(8, true);
            Thread.Sleep(chipfeederms);
            SetRelayStatus(8, false); ;
        }

       


        private void ribbonButtonSmallOpen_Click(object sender, EventArgs e)
        {
            openFileDialog1.Filter = "XML files|*.xml";
            DialogResult result = openFileDialog1.ShowDialog();

            if (result == DialogResult.OK) // Test result.
            {
                string file = openFileDialog1.FileName;
                try
                {
                    string imagefile = file.Replace(".xml", ".jpg");
                    if (System.IO.File.Exists(imagefile))
                    {
                        pictureBoxPCB.Image = new Bitmap(imagefile);
                        
                    }
                    dsMain = csvload.LoadPCB(file, dataGridView1);

                    // data loaded, now set board defaults
                    ribbonTextBoxBoardOffsetX.TextBoxText = dsMain.Tables["Settings"].Rows[0]["BoardOffsetX"].ToString(); //double

                    ribbonTextBoxBoardOffsetY.TextBoxText = dsMain.Tables["Settings"].Rows[0]["BoardOffsetY"].ToString(); //double

                    ribbonTextBoxPCBThickness.TextBoxText = dsMain.Tables["Settings"].Rows[0]["PCBThickness"].ToString(); //double //double dblPCBThickness

                    ribbonTextBoxFeedRate.TextBoxText = dsMain.Tables["Settings"].Rows[0]["FeedRate"].ToString();  //int FeedRate

                    ribbonTextBoxChipMS.TextBoxText = dsMain.Tables["Settings"].Rows[0]["chipfeederms"].ToString();  //int chipfeederms

                    dblPCBThickness = Double.Parse(dsMain.Tables["Settings"].Rows[0]["PCBThickness"].ToString());
                    FeedRate = Int32.Parse(dsMain.Tables["Settings"].Rows[0]["FeedRate"].ToString());
                    chipfeederms = Int32.Parse(dsMain.Tables["Settings"].Rows[0]["chipfeederms"].ToString());
                 }
                catch (Exception exc)
                {
                    MessageBox.Show(exc.ToString());
                }
            }
        }

     
        private void ribbonButtonStart_Click(object sender, EventArgs e)
        {
            SaveSettings();
            FeedRate = Int32.Parse(ribbonTextBoxFeedRate.TextBoxText);


            DataView rundata = new DataView();
            rundata = dtom3.ConvertToGCode(dataGridView1, Double.Parse(ribbonTextBoxBoardOffsetX.TextBoxText), Double.Parse(ribbonTextBoxBoardOffsetY.TextBoxText));

            if (backgroundWorkerDoCommand.IsBusy != true)
            {
                backgroundWorkerDoCommand.RunWorkerAsync();
            }
        }

        private void ribbonButtonEStop_Click_1(object sender, EventArgs e)
        {

            Thread thrd = new Thread(new ThreadStart(DoEStop));
            thrd.Start();
            thrd.IsBackground = true;
           
           
        }
        private void DoEStop()
        { 
            Script.DoOEMButton(1021); 
        
        }


        private void buttonHomeAll_Click(object sender, EventArgs e)
        {
            Thread thrd = new Thread(new ThreadStart(DoHomeAll));
            thrd.Start();
            thrd.IsBackground = true;
        }


        private void DoHomeAll()
        {
            Script.DoButton(24);
            Script.DoButton(25);
            Script.DoButton(22);
            Script.DoButton(23);
            Script.Code("M90198");
            
        }
        private void ribbonButtonCheckAll_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < dataGridView1.Rows.Count; i++)
            {
                dataGridView1.Rows[i].Cells["Pick"].Value = true;
            }
        }

        private void ribbonButtonUnCheckAll_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < dataGridView1.Rows.Count; i++)
            {
                dataGridView1.Rows[i].Cells["Pick"].Value = false;
            }
        }

        private void ribbonButtonChipFeederShake_Click(object sender, EventArgs e)
        {
            chipfeederms = Int32.Parse(ribbonTextBoxChipMS.TextBoxText);
            if (backgroundWorkerChipFeeder.IsBusy != true)
            {
                backgroundWorkerChipFeeder.RunWorkerAsync();
            }
        }

        private void ribbonCheckBoxLEDPicker_CheckBoxCheckChanged(object sender, EventArgs e)
        {
           
            if (ribbonCheckBoxLEDPicker.Checked)
            {
                SetRelayStatus(1, true);
            }
            else
            {
                SetRelayStatus(1, false);
            }
        }

        private void ribbonCheckBoxLEDCamera_CheckBoxCheckChanged(object sender, EventArgs e)
        {
            if (ribbonCheckBoxLEDCamera.Checked)
            {
                SetRelayStatus(2, true);
            } else {
                SetRelayStatus(2, false);
            }
        }

        private void ribbonButtonComponentEditorOpen_Click(object sender, EventArgs e)
        {
            ComponentEditor cm = new ComponentEditor();
            cm.Show(this);
        }

        private void ribbonButtonBaseCameraOpen_Click(object sender, EventArgs e)
        {
            CameraVision camvis = new CameraVision();
            camvis.Show(this);
        }

        private void ribbonButtonVisionPickerCamera_Click(object sender, EventArgs e)
        {
            CameraHead camhead = new CameraHead();
            camhead.Show(this);
        }

        private void ribbonButtonDROStart_Click(object sender, EventArgs e)
        {
            timer1.Start();
        }

        private void ribbonButtonDROStop_Click(object sender, EventArgs e)
        {
            timer1.Stop();
        }

        private void ribbonButtonManualJOGLeft_Click(object sender, EventArgs e)
        {
            Thread thrd = new Thread(new ThreadStart(RunJOGLeft));
            thrd.Start();
            thrd.IsBackground = true;
           }

        private void RunJOGLeft()
        {
            double currentpos = Double.Parse(Script.GetParam("YDRO").ToString());
            if (currentpos >= 5)
            {
                RunMach3Command(Script, "F3000");
                RunMach3Command(Script, "G01 Y" + (currentpos - 5));
            }
        }

        private void ribbonButtonManualJOGRight_Click(object sender, EventArgs e)
        {
            Thread thrd = new Thread(new ThreadStart(RunJOGRight));
            thrd.Start();
            thrd.IsBackground = true;          
        }
        private void RunJOGRight()
        {
            double currentpos = Double.Parse(Script.GetParam("YDRO").ToString());
            if (currentpos >= 0)
            {
                RunMach3Command(Script, "F3000");
                RunMach3Command(Script, "G01 Y" + (currentpos + 5));
            }
        }
        private void ribbonButtonManualJOGUp_Click(object sender, EventArgs e)
        {
            Thread thrd = new Thread(new ThreadStart(RunJOGUp));
            thrd.Start();
            thrd.IsBackground = true;   
        }
        private void RunJOGUp()
        {
            double currenty = Double.Parse(Script.GetParam("XDRO").ToString());
            if (currenty >= 0)
            {
                RunMach3Command(Script, "F3000");
                RunMach3Command(Script, "G01 X" + (currenty + 5));
            }
        }
        private void ribbonButtonManualJOGDown_Click(object sender, EventArgs e)
        {
            Thread thrd = new Thread(new ThreadStart(RunJOGDown));
            thrd.Start();
            thrd.IsBackground = true;   
        }
        private void RunJOGDown()
        {
            double currentpos = Double.Parse(Script.GetParam("XDRO").ToString());
            if (currentpos >= 5)
            {
                RunMach3Command(Script, "F3000");
                RunMach3Command(Script, "G01 X" + (currentpos - 5));
            }
        }
        private void LoadSettings() {

         ribbonTextBoxBoardOffsetX.TextBoxText  = Properties.Settings.Default.SettingBoardOffsetX.ToString(); //double

         ribbonTextBoxBoardOffsetY.TextBoxText  = Properties.Settings.Default.SettingBoardOffsetY.ToString(); //double

         ribbonTextBoxPCBThickness.TextBoxText  = Properties.Settings.Default.SettingPCBThickness.ToString(); //double //double dblPCBThickness

         ribbonTextBoxFeedRate.TextBoxText = Properties.Settings.Default.SettingFeedRate.ToString();  //int FeedRate

         ribbonTextBoxChipMS.TextBoxText = Properties.Settings.Default.SettingTimeMS.ToString();  //int chipfeederms

         dblPCBThickness = Properties.Settings.Default.SettingPCBThickness;
         FeedRate = Properties.Settings.Default.SettingFeedRate;
         chipfeederms = Properties.Settings.Default.SettingTimeMS;

     }
     private void SaveSettings()
     {
         Properties.Settings.Default.SettingBoardOffsetX = Double.Parse(ribbonTextBoxBoardOffsetX.TextBoxText);
         Properties.Settings.Default.SettingBoardOffsetY = Double.Parse(ribbonTextBoxBoardOffsetY.TextBoxText);
         Properties.Settings.Default.SettingPCBThickness = Double.Parse(ribbonTextBoxPCBThickness.TextBoxText);

         Properties.Settings.Default.SettingFeedRate = Int32.Parse(ribbonTextBoxFeedRate.TextBoxText);
         Properties.Settings.Default.SettingTimeMS = Int32.Parse(ribbonTextBoxChipMS.TextBoxText);

         // Save settings
         Properties.Settings.Default.Save();
     }
    }

    


}