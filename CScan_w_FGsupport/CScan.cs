using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using System.IO;
using System.IO.Ports;
using System.Threading;

//Aerotech crap (actually very nice and well documented libraries)
using Aerotech.A3200;
using Aerotech.A3200.Exceptions;
using Aerotech.A3200.Status;
using Aerotech.A3200.Variables;
using Aerotech.A3200.Tasks;
using Aerotech.A3200.Information;
using Aerotech.Common;
using Aerotech.Common.Collections;

//AGilent family (33500B Waveform Gen) comm. library 
using Ivi.Visa.Interop;

//NI ADQ 
using NationalInstruments;
using NationalInstruments.DAQmx;


/*Notes:
 * 
 * 
 */


namespace GUI
{
	public partial class formGUI : Form
	{
		#region Fields
        //Gantry variables/classes
		private Controller myController; //myController is used as type Controller
		private int axisIndex; //int used to track axis number (? - was here in example, might not be needed)
		private int taskIndex; //int used to track current task (? - was here in example, might not be needed)
        int axisNum = 0; //1,2,3 = x,y,z (used for controlling which axis to move in function: CNCOp)
        string axisLetter; // X, Y, Z (used to translate axisNum into an A3200 axisMask for input into movement function)
        int globalFlag = 0; //Global flag used throughout program for saftey.  Syntax: 0 = normal operation, 1 = halt
        int axisNumRow = 0; //Like axisNum but for grid scanning
        string axisLetterRow; // Like axisLetter but for grid scanning (assigned based on axisNumRow in a switch statement)

        //Function Generator (AGILENT 33500B Series Waveform Generator)
        ResourceManager rMgr = new ResourceManagerClass(); //ResourceManager frequently used so it is given shorthand
        FormattedIO488 src = new FormattedIO488Class(); //FormattedIO488 frequently used even though not sure what an IO488 is.

        FormattedIO488 FWG33220 = new FormattedIO488Class(); //FormattedIO488 frequently used even though not sure what an IO488 is.
        //serial port
        private SerialPort SerialLockin=new SerialPort();
        private SerialPort SerialMcu=new SerialPort();

        private string serindata;
        //
        //work function  added by chaofeng 02262015
        
        string lockinreaddata=null;
        
        

        //work function end here 
        double F1, F2, A1, A2, P1, P2; //Freq, amp, and phase of FG for use in TryParse
        int out1, out2; //Int for output of FG, 1=true, 0=false

        //variable for adc
        double [,] adcresult;
        bool flagadcrun = false;
		#endregion Fields

		#region Constructors

		public formGUI() //DON'T TOUCH THIS.
		{
			InitializeComponent();
		}

		private void formGUI_Load(object sender, EventArgs e)
		{
			checkControllerConnected.Checked = false; //Checkbox controller connected unchecked because Controller is not connected upon startup
			EnableControls(false); //For program robustness, disable controls to avoid throwing exceptions
            checkFGConnected.Checked = false; //Checkbox FG connected unchecked bc connection to FG is not established upon startup
		}

		private void formGUI_FormClosing(object sender, FormClosingEventArgs e)
		{
			//Disconnect from controller
			Controller.Disconnect();
		}

		#endregion Constructors

		#region Methods

		/// <summary>
		/// Enable or disable control groups
		/// </summary>
		private void EnableControls(bool enable) //Allows enabling a set group of groupboxes with one line later on
		{
			groupAxis.Enabled = enable;
			groupTask.Enabled = enable;
			groupGlobalDoubles.Enabled = enable;
		}

		/// <summary>
		/// Process task state arrived event
		/// </summary>
		private void SetTaskState(NewTaskStatesArrivedEventArgs e)
		{
			labelTaskState.Text = e.TaskStates[this.taskIndex].ToString();
		}

		/// <summary>
		/// Process DiagPacket (axis state in it) arrived event
		/// </summary>
		private void SetAxisState(NewDiagPacketArrivedEventArgs e)
		{
            //old code that came with the example (Don't delete in case other functionality desired)
			labelAxisState.Text = e.Data[this.axisIndex].DriveStatus.Enabled.ToString();
			labelAxisHomed.Text = e.Data[this.axisIndex].AxisStatus.Homed.ToString();
			labelAxisFault.Text = (!e.Data[this.axisIndex].AxisFault.None).ToString();
			labelAxisPosition.Text = e.Data[this.axisIndex].PositionFeedback.ToString();
			labelAxisSpeed.Text = e.Data[this.axisIndex].VelocityFeedback.ToString();
            
            //X Axis State (update "Control Panel" labels on NewDiagPacketArrivedEventArgs -- new status packet arrived, from what I understand)
            labelXpos.Text = e.Data["X"].PositionFeedback.ToString();
            labelXspeed.Text = e.Data["X"].VelocityFeedback.ToString();
            labelXen.Text = e.Data["X"].DriveStatus.Enabled.ToString();
            labelXhomed.Text = e.Data["X"].AxisStatus.Homed.ToString();
            labelXfault.Text = (!e.Data["X"].AxisFault.None).ToString(); //must use ! operator because this is the AxisFault.None not "AxisFault.true" or something

            //Y Axis State
            labelYpos.Text = e.Data["Y"].PositionFeedback.ToString();
            labelYspeed.Text = e.Data["Y"].VelocityFeedback.ToString();
            labelYen.Text = e.Data["Y"].DriveStatus.Enabled.ToString();
            labelYhomed.Text = e.Data["Y"].AxisStatus.Homed.ToString();
            labelYfault.Text = (!e.Data["Y"].AxisFault.None).ToString();

            //Z Axis State
            labelZpos.Text = e.Data["Z"].PositionFeedback.ToString();
            labelZspeed.Text = e.Data["Z"].VelocityFeedback.ToString();
            labelZen.Text = e.Data["Z"].DriveStatus.Enabled.ToString();
            labelZhomed.Text = e.Data["Z"].AxisStatus.Homed.ToString();
            labelZfault.Text = (!e.Data["Z"].AxisFault.None).ToString();

		}

		#endregion Methods

        //Note: a lot of things in WindowsEvents are unused. Since this was built off the example program, it is recommended that this be left here.
		#region WindowsEvents

		private void buttonConnectController_Click(object sender, EventArgs e)
		{
			try
			{
				// Connect to A3200 controller.  
				this.myController = Controller.Connect();
				checkControllerConnected.Checked = true;
				EnableControls(true); //call first function: EnableControls to enable all CNC control stuff 

				// populate axis names (not used in new layout -- don't delete in case additional functionality required)
				foreach (AxisInfo axis in this.myController.Information.Axes)
				{
					comboAxis.Items.Add(axis.Name);
				}
				this.axisIndex = 0;
				comboAxis.SelectedIndex = this.axisIndex;

				// populate task names (unused)
                foreach (Aerotech.A3200.Tasks.Task task in this.myController.Tasks)
				{
					if (task.State != TaskState.Inactive)
					{
						comboTask.Items.Add(task.Name.ToString());
					}
				}
				// Task 0 is reserved
				this.taskIndex = 1;
				comboTask.SelectedIndex = this.taskIndex - 1;

				// register task state and diagPackect arrived events
				this.myController.ControlCenter.TaskStates.NewTaskStatesArrived += new EventHandler<NewTaskStatesArrivedEventArgs>(TaskStates_NewTaskStatesArrived);
				this.myController.ControlCenter.Diagnostics.NewDiagPacketArrived += new EventHandler<NewDiagPacketArrivedEventArgs>(Diagnostics_NewDiagPacketArrived);
			}
			catch (A3200Exception exception)
			{
				labelErrorMessage.Text = exception.Message;
			}
		}

		private void buttonEnableAxis_Click(object sender, EventArgs e)
		{
			try
			{
				this.myController.Commands[this.taskIndex].Axes[this.axisIndex].Motion.Enable();
			}
			catch (A3200Exception exception)
			{
				labelErrorMessage.Text = exception.Message;
			}
		}

		private void buttonDisableAxis_Click(object sender, EventArgs e)
		{
			try
			{
				this.myController.Commands[this.taskIndex].Axes[this.axisIndex].Motion.Disable();
			}
			catch (A3200Exception exception)
			{
				labelErrorMessage.Text = exception.Message;
			}
		}

		private void comboAxis_SelectedIndexChanged(object sender, EventArgs e)
		{
			this.axisIndex = comboAxis.SelectedIndex;
		}

		private void comboTask_SelectedIndexChanged(object sender, EventArgs e)
		{
			// Task 0 is reserved
			this.taskIndex = comboTask.SelectedIndex + 1;
		}

		private void buttonExecuteGenericString_Click(object sender, EventArgs e)
		{
			try
			{
				this.myController.Commands[this.taskIndex].Execute(textGenericString.Text);
			}
			catch (A3200Exception exception)
			{
				labelErrorMessage.Text = exception.Message;
			}
		}

		private void buttonRunProgram_Click(object sender, EventArgs e)
		{
			try
			{
				this.myController.Tasks[this.taskIndex].Program.Run(textProgram.Text);
			}
			catch (A3200Exception exception)
			{
				labelErrorMessage.Text = exception.Message;
			}
		}

		private void buttonStopProgram_Click(object sender, EventArgs e)
		{

			try
			{
				this.myController.Tasks[this.taskIndex].Program.Stop();
			}
			catch (A3200Exception exception)
			{
				labelErrorMessage.Text = exception.Message;
			}
		}

		private void buttonClearErrorMessage_Click(object sender, EventArgs e)
		{
			labelErrorMessage.Text = "";
		}

		/// <summary>
		/// Hold down Freerun button, the axis will move
		/// </summary>
		//Mouse up and downs
        private void buttonFreerunCW_MouseDown(object sender, MouseEventArgs e)
		{
			try
			{
				this.myController.Commands[this.taskIndex].Motion.FreeRun(this.axisIndex, double.Parse(textFreerunSpeed.Text));
			}
			catch (A3200Exception exception)
			{
				labelErrorMessage.Text = exception.Message;
			}
		}

		/// <summary>
		/// Release Freerun button, the axis will stop
		/// </summary>
		private void buttonFreerunCW_MouseUp(object sender, MouseEventArgs e)
		{
			try
			{
				this.myController.Commands[this.taskIndex].Motion.FreeRun(this.axisIndex, 0);
			}
			catch (A3200Exception exception)
			{
				labelErrorMessage.Text = exception.Message;
			}
		}

		/// <summary>
		/// Hold down Freerun button, the axis will move
		/// </summary>
		private void buttonFreerunCCW_MouseDown(object sender, MouseEventArgs e)
		{
			try
			{
				this.myController.Commands[this.taskIndex].Motion.FreeRun(this.axisIndex, -double.Parse(textFreerunSpeed.Text));
			}
			catch (A3200Exception exception)
			{
				labelErrorMessage.Text = exception.Message;
			}
		}

		/// <summary>
		/// Release Freerun button, the axis will stop
		/// </summary>
		private void buttonFreerunCCW_MouseUp(object sender, MouseEventArgs e)
		{
			try
			{
				this.myController.Commands[this.taskIndex].Motion.FreeRun(this.axisIndex, 0);
			}
			catch (A3200Exception exception)
			{
				labelErrorMessage.Text = exception.Message;
			}
		}
        /// <summary>
        /// //
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
		private void buttonUpdateGlobalDoubles_Click(object sender, EventArgs e)
		{
			listGlobalDouble.Items.Clear();
			// add all global doubles
			foreach (Aerotech.A3200.Variables.TypedVariable<double> GlobalDouble in this.myController.Variables.Global.Doubles)
			{
				listGlobalDouble.Items.Add(GlobalDouble.Value);
			}
		}

		#endregion WindowsEvents

		#region ControllerEvents

		/// <summary>
		/// Handle task state arrived event. Invoke SetTaskState to process data
		/// </summary>
		private void TaskStates_NewTaskStatesArrived(object sender, NewTaskStatesArrivedEventArgs e)
		{
			try
			{
				//URL: http://msdn.microsoft.com/en-us/library/ms171728.aspx
				//How to: Make Thread-Safe Calls to Windows Forms Controls
				this.Invoke(new Action<NewTaskStatesArrivedEventArgs>(SetTaskState), e);
			}
			catch
			{
			}
		}

		/// <summary>
		/// Handle DiagPacket (axis state in it) arrived event. Invoke SetAxisState to process data
		/// </summary>
		private void Diagnostics_NewDiagPacketArrived(object sender, NewDiagPacketArrivedEventArgs e)
		{
			try
			{
				//URL: http://msdn.microsoft.com/en-us/library/ms171728.aspx
				//How to: Make Thread-Safe Calls to Windows Forms Controls
				this.Invoke(new Action<NewDiagPacketArrivedEventArgs>(SetAxisState), e);
			}
			catch
			{
			}
		}

		#endregion ControllerEvents


        //ask Matt how to delete these properly asfeaseasesef
        #region oldshit
        private void checkControllerConnected_CheckedChanged(object sender, EventArgs e)
        {
            //MessageBox.Show("VS2013 sucks");
        }


        #endregion oldshit 
        //From this point on all code is new and should be well documented.
        
        //X Y and Z stuff concerns jogging of axes
        #region X stuff
        
        //Handle X-axis Left and Right buttons 
         private void button1_MouseDown(object sender, EventArgs e) //dont know why designer didn't update to "buttonL"
        {
            try
            {
                this.myController.Commands[this.taskIndex].Motion.FreeRun(this.axisIndex, -double.Parse(textBoxSpeed.Text));
                //Alternatively, use: myController.Commands.Motion.FreeRun(string AxisLetter, double speed);
            }
            catch (A3200Exception exception)
            {
                labelErrorMessage.Text = exception.Message;
            }
        }
         private void button1_MouseUp(object sender, EventArgs e)
        {
            try
            {
                this.myController.Commands[this.taskIndex].Motion.FreeRun(this.axisIndex, 0);
            }
            catch (A3200Exception exception)
            {
                labelErrorMessage.Text = exception.Message;
            }
        }

         private void buttonR_MouseDown(object sender, MouseEventArgs e)
         {
             try
             {
                 this.myController.Commands[this.taskIndex].Motion.FreeRun("X", double.Parse(textBoxSpeed.Text));
             }
             catch (A3200Exception exception)
             {
                 labelErrorMessage.Text = exception.Message;
             }
         }

         private void buttonR_MouseUp(object sender, MouseEventArgs e)
         {

             try
             {
                 this.myController.Commands[this.taskIndex].Motion.FreeRun("X", 0);
             }
             catch (A3200Exception exception)
             {
                 labelErrorMessage.Text = exception.Message;
             }
         }

        //Handle X-axis Enable and Disable
         private void buttonXen_Click(object sender, EventArgs e)
         {
             try
             {
                 this.myController.Commands[this.taskIndex].Axes["X"].Motion.Enable();
             }
             catch (A3200Exception exception)
             {
                 labelErrorMessage.Text = exception.Message;
             }
         }


         private void buttonXdis_Click(object sender, EventArgs e)
         {
             try
             {
                 this.myController.Commands[this.taskIndex].Axes["X"].Motion.Disable();
             }
             catch (A3200Exception exception)
             {
                 labelErrorMessage.Text = exception.Message;
             }
         }

        #endregion X stuff

        #region Y stuff

         //Handle Y-axis Left and Right buttons 
         private void buttonFW_MouseDown(object sender, MouseEventArgs e)
         {
             try
             {
                 this.myController.Commands[this.taskIndex].Motion.FreeRun("Y", -double.Parse(textBoxSpeed.Text));
             }
             catch (A3200Exception exception)
             {
                 labelErrorMessage.Text = exception.Message;
             }
         }

         private void buttonFW_MouseUp(object sender, MouseEventArgs e)
         {
             try
             {
                 this.myController.Commands[this.taskIndex].Motion.FreeRun("Y", 0);
             }
             catch (A3200Exception exception)
             {
                 labelErrorMessage.Text = exception.Message;
             }
         }

         private void buttonBK_MouseDown(object sender, MouseEventArgs e)
         {
             try
             {
                 this.myController.Commands[this.taskIndex].Motion.FreeRun("Y", double.Parse(textBoxSpeed.Text));
             }
             catch (A3200Exception exception)
             {
                 labelErrorMessage.Text = exception.Message;
             }
         }

         private void buttonBK_MouseUp(object sender, MouseEventArgs e)
         {
             try
             {
                 this.myController.Commands[this.taskIndex].Motion.FreeRun("Y", 0);
             }
             catch (A3200Exception exception)
             {
                 labelErrorMessage.Text = exception.Message;
             }
         }

         //Handle Y-axis Enable and Disable
         private void buttonYen_Click(object sender, EventArgs e)
         {
             try
             {
                 //this.myController.Commands[this.taskIndex].Axes["Y"].Motion.Enable();
                 //Had intermittent problems with above method so now use:
                 myController.Commands.Axes["Y"].Motion.Enable();
             }
             catch (A3200Exception exception)
             {
                 labelErrorMessage.Text = exception.Message;
             }
         }

         private void buttonYdis_Click(object sender, EventArgs e)
         {
             try
             {
                 //this.myController.Commands[this.taskIndex].Axes["Y"].Motion.Disable();
                 myController.Commands.Axes["Y"].Motion.Disable();
                 
             }
             catch (A3200Exception exception)
             {
                 labelErrorMessage.Text = exception.Message;
             }
         }

         #endregion Y stuff

        #region Z stuff

         //Handle Z-axis Left and Right buttons 
         private void buttonDN_MouseDown(object sender, MouseEventArgs e)
         {
             try
             {
                 this.myController.Commands[this.taskIndex].Motion.FreeRun("Z", -double.Parse(textBoxSpeed.Text));
             }
             catch (A3200Exception exception)
             {
                 labelErrorMessage.Text = exception.Message;
             }
         }

         private void buttonDN_MouseUp(object sender, MouseEventArgs e)
         {
             try
             {
                 this.myController.Commands[this.taskIndex].Motion.FreeRun("Z", 0);
             }
             catch (A3200Exception exception)
             {
                 labelErrorMessage.Text = exception.Message;
             }
         }

         private void buttonUP_MouseDown(object sender, MouseEventArgs e)
         {
             try
             {
                 this.myController.Commands[this.taskIndex].Motion.FreeRun("Z", double.Parse(textBoxSpeed.Text));
             }
             catch (A3200Exception exception)
             {
                 labelErrorMessage.Text = exception.Message;
             }
         }

         private void buttonUP_MouseUp(object sender, MouseEventArgs e)
         {
             try
             {
                 this.myController.Commands[this.taskIndex].Motion.FreeRun("Z", 0);
             }
             catch (A3200Exception exception)
             {
                 labelErrorMessage.Text = exception.Message;
             }
         }

         //Handle Z-axis Enable and Disable
         private void buttonZen_Click(object sender, EventArgs e)
         {
             try
             {
                 this.myController.Commands[this.taskIndex].Axes["Z"].Motion.Enable();
             }
             catch (A3200Exception exception)
             {
                 labelErrorMessage.Text = exception.Message;
             }
         }

         private void buttonZdis_Click(object sender, EventArgs e)
         {
             try
             {
                 this.myController.Commands[this.taskIndex].Axes["Z"].Motion.Disable();
             }
             catch (A3200Exception exception)
             {
                 labelErrorMessage.Text = exception.Message;
             }
         }
        
        #endregion Z stuff


        //Contains the main movement function
         #region MoarFunc

        public void UpdateButtons(int out1, int out2) //Updates FG output buttons
         {
             if (out1 == 1)
             {
                 //Output 1 is on
                 buttonOut1.Text = ("Output (ON)");
                 buttonOut1.BackColor = Color.Lime;
             }
             else if (out1 == 0)
             {
                 //Output 1 is off
                 buttonOut1.Text = ("Output (OFF)");
                 buttonOut1.BackColor = Color.Gray;
             }
             else
                 MessageBox.Show("Error: Buttons failed to update.  Color may not indicate true state.");

            if (out2 == 1)
            {
                //Output 2 is on
                buttonOut2.Text = ("Output (ON)");
                buttonOut2.BackColor = Color.Lime;
            }
            else if (out2 == 0)
            {
                //Output 2 is off
                buttonOut2.Text = ("Output (OFF)");
                buttonOut2.BackColor = Color.Gray;
            }
            else
                MessageBox.Show("Error: Buttons failed to update.  Color may not indicate true state.");
         }

         public void TimeWait(double pauseTime) //This neat little block delays the loop by (double) pauseTime seconds for first:last data read
         {
             DateTime Tthen = DateTime.Now; //Tthen = current time
             do
             {
                 Application.DoEvents(); //Continue generic operation (stuck in a loop), while
             } while (Tthen.AddSeconds(pauseTime) > DateTime.Now); // Tthen + pausetime is ahead of now.
         } //Thread will not be put to sleep so GUI will remain responsive so long as it can be.
        
         private void CNCOp(double Xdist, double Ydist, double Zdist, double Xstep, double Ystep, double Zstep, int numDatapoints, double pauseTime) //Main movement function.  Executes when buttonExe is clicked.
         {
             double stepSize = 0; //Size of step to be iterated through
             //int flag = 0; //Tracks user input, where 1 = input distance, 2 = input step size 

             string filename = rtbFilefoder.Text;
             string[] sfre = rtbFrequencies.Text.Split(',');
             int nfre = sfre.Length;
             int nsensor = Convert.ToInt16(rtbNumberofsensors.Text);

             
             if ((Xstep != 0) || (Ystep != 0) || (Zstep != 0)) //if the user is inputting a step size and number of datapoints
             {
                if (Xstep != 0)
                { 
                    axisLetter = "X";
                         stepSize = Xstep;
                }
                else if (Ystep != 0)
                {
                    axisLetter = "Y";
                         stepSize = Ystep;
                }
                else if (Zstep != 0)
                {
                    axisLetter = "Z";
                    stepSize = Zstep;
                }
                else
                {
                    MessageBox.Show("Congrats, you somehow managed to completely screw this program, narrowly missing else if statements and catches.  You should feel proud of yourself.  Unfortunately execution has been halted.");
                    return;
                }
                        
              
             }
             //After movement has been determined, check to ensure the E-Stop hasn't been pressed. (Saftey feature!!)
             if (globalFlag == 1)
                 return;

                         

             //get data

             run1line(nfre, sfre, nsensor, pauseTime, filename);
             for (int i = 1; i < numDatapoints; i++)
             {
                 
                 

                 TimeWait(pauseTime); //Calls function to delay without pausing thread execution like sleep(n) does
                
                 //Right before movement, again make sure E-Stop hasn't been pressed
                 if (globalFlag == 1)
                     return;

                 //Move to next point
                 try
                 {
                     this.myController.Commands.Motion.Linear(axisLetter, stepSize);
                 }
                 catch (A3200Exception exception)
                 {
                     labelErrorMessage.Text = exception.Message + "Here is a helplink: " + exception.HelpLink;
                     MessageBox.Show("Houston, we have a problem.  Your scanner dun fucked up."); //CHANGE BEFORE SUBMISSION
                     break;
                 }
                run1line(nfre, sfre, nsensor, pauseTime, filename);

             }   
 
             //move back to initial position
             //Move to next point
             try
             {
                 this.myController.Commands.Motion.Linear(axisLetter, -(numDatapoints-1)*stepSize);
             }
             catch (A3200Exception exception)
             {
                 labelErrorMessage.Text = exception.Message + "Here is a helplink: " + exception.HelpLink;
                 MessageBox.Show("Houston, we have a problem.  Your scanner dun fucked up."); //CHANGE BEFORE SUBMISSION                 
             }
 

          }

         private void GScan (int rowX, int rowY, int rowZ, double rowDist)
         {
             int rows=0;

             //Decide which axis should be swept across
             switch (axisNumRow)
             {
                 case 0: //No axes selected
                     MessageBox.Show(new Form() { TopMost = true }, "Cannot move machine without directions. PROGRAM ME DAMNIT!"); //CHANGE BEFORE SUBMISSION
                     rows = 0;
                     break;

                 case 1: //X Coverage
                     axisLetterRow = "X";
                     rows = rowX;
                     break;

                 case 2: //Y Coverage
                     axisLetterRow = "Y";
                     rows = rowY;
                     break;

                 case 3: //Z Coverage
                     axisLetterRow = "Z";
                     rows = rowZ;
                     break;
             }

             //Sweep across axisLetterRow, moving forward and backwards orthagonally to it
             for (int j = 1; j < rows + 1; j++)
             {
                 labelDiagnostic.Text = "Current j: " + j.ToString();

                 //Call CNCOp with correct parameters
                 CNCOp(double.Parse(textBoxXdist.Text), double.Parse(textBoxYdist.Text), double.Parse(textBoxZdist.Text),
                        double.Parse(textBoxXstep.Text), double.Parse(textBoxYstep.Text), double.Parse(textBoxZstep.Text),
                        int.Parse(textBoxDatapoints.Text), double.Parse(textBoxPausetime.Text));

                 //Move back
                 CNCOp(-double.Parse(textBoxXdist.Text), -double.Parse(textBoxYdist.Text), -double.Parse(textBoxZdist.Text),
                        -double.Parse(textBoxXstep.Text), -double.Parse(textBoxYstep.Text), -double.Parse(textBoxZstep.Text),
                        2, 0);

                 //Move to next row and repeat process so long as not on last iteration
                 if (j != rows)
                 {
                     labelDiagnostic.Text = "Current j: " + j.ToString() + "IF statement entered! why the fuck arent you moving";

                     /*
                     CNCOp(double.Parse(textBoxXdist.Text), double.Parse(textBoxYdist.Text), double.Parse(textBoxZdist.Text),
                        double.Parse(textBoxXstep.Text), double.Parse(textBoxYstep.Text), double.Parse(textBoxZstep.Text),
                        int.Parse(textBoxDatapoints.Text), double.Parse(textBoxPausetime.Text)); 
                      */

                     //Hard coded version for initial testing. Moves 5 mm over on x axis with 2 DP for just one movement
                     //CNCOp(5, 0, 0, 0, 0, 0, 2, 0); //IDK WHY THE F THIS WON't WORK.

                     //put saftey measures in here for E stop
                     try
                     {
                         myController.Commands.Motion.Linear(axisLetterRow, rowDist); //Move over rowDist in direction of axisLetterRow
                     }
                     catch (A3200Exception exception)
                     {
                         labelErrorMessage.Text = exception.Message;
                     }

                     labelDiagnostic.Text = "Current j: " + j.ToString() + "CNCOp about to be called, then pausing for 1 sec";

                 }
                 else if (j == rows)
                 {
                     labelDiagnostic.Text = "Current j: " + j.ToString() + "FIFTH J, show the damn messagebox";
                     //CNCOp(-10, 0, 0, 0, 0, 0, 2, 0);
                     try
                     {
                         myController.Commands.Motion.Linear(axisLetterRow, -rowDist*(rows-1));
                     }
                     catch (A3200Exception exception)
                     {
                         labelErrorMessage.Text = exception.Message;
                     }
                 }


             }
         }
         
         private void SetFG(int channel, int property, double value)
         {
             
         }
         #endregion MoarFunc


         private void buttonExe_Click(object sender, EventArgs e)
         {
             preparetowork(true,true); 
             CNCOp(double.Parse(textBoxXdist.Text), double.Parse(textBoxYdist.Text), double.Parse(textBoxZdist.Text), 
                 double.Parse(textBoxXstep.Text), double.Parse(textBoxYstep.Text), double.Parse(textBoxZstep.Text), 
                 int.Parse(textBoxDatapoints.Text), double.Parse(textBoxPausetime.Text)); 
         }

         private void buttonStop_Click(object sender, EventArgs e) //check functionality of this function
         {
             globalFlag = 1; //Throw a 1 to tell any functions about to execute to abort 
           
             //Determine which axis is running, and thus must be stopped   
             switch (axisNum)
             {
                 case 0: //No axes selected
                     MessageBox.Show(new Form() { TopMost = true }, "Machine is not moving.. at least it shouldn't be."); 
                     break;

                 case 1: //X movement
                     axisLetter = "X";                   
                     break;

                 case 2: //Y movement
                     axisLetter = "Y";
                     break;

                 case 3: //Z movement
                     axisLetter = "Z";                     
                     break;
             }
             try
             {
                 //this.myController.Commands[this.taskIndex].Motion.Abort(axisLetter);
                 myController.Commands.Motion.Linear(axisLetter, 0);
             }
             catch(A3200Exception exception)
             {
                 MessageBox.Show("Machine is FUBAR and out of control!  Kill power IMMEDIATELY to prevent damage and serious injury!", "A3200 XTreme Error!", MessageBoxButtons.OK, MessageBoxIcon.Stop);
                 labelErrorMessage.Text = exception.Message;
             }
         }
        
         private void buttonGScan_Click(object sender, EventArgs e)
         {
            
         }




         //Region to ensure code doesn't do bad things
         #region robustness //fix up to block null entries

         private void textBoxXdist_TextChanged(object sender, EventArgs e)
         {
             double comp; //Necessary because must have a double for TryParse to out with
             bool result = double.TryParse(textBoxXdist.Text, out comp); //Tryparse (tryparsethisstring, out outvar)
             if (comp == 0) //if X distance box is not used, enable all others
             {
                 textBoxXRows.Enabled = true;
                 textBoxYdist.Enabled = true;
                 textBoxZdist.Enabled = true;
                 groupBoxStepsize.Enabled = true;
                 axisNum = 0; //No axes used in this state
                 
             }
             else  //X distance used, disable others (for now)
             {
                 textBoxXRows.Enabled = false; //Disable input for grid scanning across same row as line scan
                 textBoxYdist.Enabled = false;
                 textBoxZdist.Enabled = false;
                 groupBoxStepsize.Enabled = false; //Disable the step size input if using distance
                 axisNum = 1; //Corresponds to a movement on the X axis
             }
             
         }

         private void textBoxXstep_TextChanged(object sender, EventArgs e)
         {
             double comp;
             bool result = double.TryParse(textBoxXstep.Text, out comp);
             if (comp == 0) //if X stepsize not used, enable all
             {
                 textBoxXRows.Enabled = true;
                 textBoxYstep.Enabled = true;
                 textBoxZstep.Enabled = true;
                 groupBoxDistance.Enabled = true;
                 axisNum = 0;
             }
             else //if X stepsize used, disable all others
             {
                 textBoxXRows.Enabled = false;
                 textBoxYstep.Enabled = false;
                 textBoxZstep.Enabled = false;
                 groupBoxDistance.Enabled = false; //disable distance input if using steps
                 axisNum = 1; //Corresponds to a movement on the X axis (again)
             }

         }

         private void textBoxXRows_TextChanged(object sender, EventArgs e)
         {
             int comp;
             bool result = int.TryParse(textBoxXRows.Text, out comp);
             
             //null is not what is returned from empty textbox. figure this out
             if ((result == false) && (comp != null)) //Make sure user input a type Int for number of rows.  Cannot have fraction of row.
                 MessageBox.Show("Input type Int for number of rows.");

             if (comp == 0)
             {
                 textBoxYRows.Enabled = true;
                 textBoxZRows.Enabled = true;
                 axisNumRow = 0;
             }
             else
             {
                 textBoxYRows.Enabled = false;
                 textBoxZRows.Enabled = false;
                 axisNumRow = 1;
             }
         }

        

         private void textBoxYdist_TextChanged(object sender, EventArgs e)
         {
             double comp;
             bool result = double.TryParse(textBoxYdist.Text, out comp);
             if (comp == 0) 
             {
                 textBoxYRows.Enabled = true;
                 textBoxXdist.Enabled = true;
                 textBoxZdist.Enabled = true;
                 groupBoxStepsize.Enabled = true;
                 axisNum = 0;
             }
             else
             {
                 textBoxYRows.Enabled = false;
                 textBoxXdist.Enabled = false;
                 textBoxZdist.Enabled = false;
                 groupBoxStepsize.Enabled = false;
                 axisNum = 2; //Corresponds to a movement on the Y axis
             }

         }


         private void textBoxYstep_TextChanged(object sender, EventArgs e)
         {
             double comp;
             bool result = double.TryParse(textBoxYstep.Text, out comp);
             if (comp == 0)
             {
                 textBoxYRows.Enabled = true;
                 textBoxXstep.Enabled = true;
                 textBoxZstep.Enabled = true;
                 groupBoxDistance.Enabled = true;
                 axisNum = 0;
             }
             else
             {
                 textBoxYRows.Enabled = false;
                 textBoxXstep.Enabled = false;
                 textBoxZstep.Enabled = false;
                 groupBoxDistance.Enabled = false;
                 axisNum = 2; // Y
             }
         }

         private void textBoxYRows_TextChanged(object sender, EventArgs e)
         {
             int comp;
             bool result = int.TryParse(textBoxYRows.Text, out comp);

             if (result == false) //Make sure user input a type Int for number of rows.  Cannot have fraction of row.
                 MessageBox.Show("Input type Int for number of rows.");

             if (comp == 0)
             {
                 textBoxXRows.Enabled = true;
                 textBoxZRows.Enabled = true;
                 axisNumRow = 0;
             }
             else
             {
                 textBoxXRows.Enabled = false;
                 textBoxZRows.Enabled = false;
                 axisNumRow = 2;
             }
         }

        

         private void textBoxZdist_TextChanged(object sender, EventArgs e)
         {
             double comp;
             bool result = double.TryParse(textBoxZdist.Text, out comp);
             if (comp == 0)
             {
                 textBoxZRows.Enabled = true;
                 textBoxXdist.Enabled = true;
                 textBoxYdist.Enabled = true;
                 groupBoxStepsize.Enabled = true;
                 axisNum = 0;
             }
             else
             {
                 textBoxZRows.Enabled = false;
                 textBoxXdist.Enabled = false;
                 textBoxYdist.Enabled = false;
                 groupBoxStepsize.Enabled = false;
                 axisNum = 3; //Corresponds to a movement on the Z axis
             }
         }

         


         private void textBoxZstep_TextChanged(object sender, EventArgs e)
         {
             double comp;
             bool result = double.TryParse(textBoxZstep.Text, out comp);
             if (comp == 0)
             {
                 textBoxZRows.Enabled = true;
                 textBoxXstep.Enabled = true;
                 textBoxYstep.Enabled = true;
                 groupBoxDistance.Enabled = true;
                 axisNum = 0;
             }
             else
             {
                 textBoxZRows.Enabled = false;
                 textBoxXstep.Enabled = false;
                 textBoxYstep.Enabled = false;
                 groupBoxDistance.Enabled = false;
                 axisNum = 3; // Z
             }
         }

         private void textBoxZRows_TextChanged(object sender, EventArgs e)
         {
             int comp;
             bool result = int.TryParse(textBoxZRows.Text, out comp);

             if (result == false) //Make sure user input a type Int for number of rows.  Cannot have fraction of row.
                 MessageBox.Show("Input type Int for number of rows.");

             if (comp == 0)
             {
                 textBoxYRows.Enabled = true;
                 textBoxXRows.Enabled = true;
                 axisNumRow = 0;
             }
             else
             {
                 textBoxYRows.Enabled = false;
                 textBoxXRows.Enabled = false;
                 axisNumRow = 3;
             }
         }
        #endregion robustness 

         private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
         {
             MessageBox.Show("CScan has been developed in C# as a robust scanning option for the A3200 Motion Controller so that we can leave LabVIEW back on the Windows XP machine (Maunakea).");
         }

         private void someNotesToolStripMenuItem_Click(object sender, EventArgs e)
         {
             MessageBox.Show("Below are some notes about this program:\nTo use:\nAfter connecting to the controller in the first tab, switch to the Line Scan tab and input some line scanning parameters.  If you would just like to scan in a line, hit the button.  If you want grid scanning, go to the GScan tab and input parameters there.  You must fill in the Line Scan parameters because they are used to describe each line in the grid scan.\n\nTroubleshooting:\nHouston is having problems: Try disabling and re-enabling axes.  For whatever reason it seems that after using Jog controls, the handling of the axes is not release or something so you must disable them and re enable them.");
         }

         #region AgilentFG
         public void buttonConnectFG_Click(object sender, EventArgs e)
         {

             string srcAddress = textBoxFGaddress.Text;
             src.IO = (IMessage)rMgr.Open(srcAddress, AccessMode.NO_LOCK, 2000, null);
             src.IO.Timeout = 3000;

             checkFGConnected.Checked = true;

             //Synchronizes phases (always desired ?)
             src.WriteString(":SOUR2:PHAS:SYNC ", true); 

             //Updates all textboxes with current values
             src.WriteString(":SOUR1:FREQ? ", true);
             textBoxF1.Text = src.ReadString();
             src.WriteString(":SOUR2:FREQ? ", true);
             textBoxF2.Text = src.ReadString();
             src.WriteString(":SOUR1:VOLT? ", true);
             textBoxA1.Text = src.ReadString();
             src.WriteString(":SOUR2:VOLT? ", true);
             textBoxA2.Text = src.ReadString();
             src.WriteString(":SOUR1:PHAS? ", true);
             textBoxP1.Text = src.ReadString();
              
             src.WriteString(":SOUR2:PHAS? ", true);
             
             textBoxP2.Text = src.ReadString();

             //Updates buttons
             src.WriteString(":OUTP1:STAT? ", true);
             out1 = int.Parse(src.ReadString());
             src.WriteString(":OUTP2:STAT? ", true);
             out2 = int.Parse(src.ReadString());
             UpdateButtons(out1, out2); //See #region MoarFunc

         }

         private void buttonF1get_Click(object sender, EventArgs e)
         {
             //nothing as of now
         }
         private void setfrquency(double fre1, double fre2)
         {
             src.WriteString(":SOUR1:FREQ " + fre1, true);
             src.WriteString(":SOUR2:FREQ " + fre2, true);
             src.WriteString(":SOUR1:PHAS:SYNC ", true);
         }
         private void buttonF1set_Click(object sender, EventArgs e)
         {
             bool result = double.TryParse(textBoxF1.Text, out F1);
             if (result == true)
             {
                 src.WriteString(":SOUR1:FREQ " + F1, true);
             }
             else
             {
                 MessageBox.Show("Error: Input type double Frequency (Hz).");
                 return;
             }
             //ins update string 
                

         }

         private void buttonF2get_Click(object sender, EventArgs e)
         {

         }

         private void buttonF2Set_Click(object sender, EventArgs e)
         {
             bool result = double.TryParse(textBoxF2.Text, out F2);
             if (result == true)
             {
                 src.WriteString(":SOUR2:FREQ " + F2, true);
             }
             else
             {
                 MessageBox.Show("Error: Input type double Frequency (Hz).");
                 return;
             }
                 
             
         }

         private void buttonA1get_Click(object sender, EventArgs e)
         {

         }

         private void buttonA1set_Click(object sender, EventArgs e)
         {
             bool result = double.TryParse(textBoxA1.Text, out A1);
             if (result == true)
             {
                 src.WriteString(":SOUR1:VOLT " + A1, true);
             }
             else
             {
                 MessageBox.Show("Error: Input type double Amplitude (Vpp).");
                 return;
             }
                 
         }

         private void buttonA2get_Click(object sender, EventArgs e)
         {

         }

         private void buttonA2set_Click(object sender, EventArgs e)
         {
             bool result = double.TryParse(textBoxA2.Text, out A2);
             if (result == true)
             {
                 src.WriteString(":SOUR2:VOLT " + A2, true);
             }
             else
             {
                 MessageBox.Show("Error: Input type double Amplitude (Vpp).");
                 return;
             }
                 
         }

         private void buttonP1get_Click(object sender, EventArgs e)
         {

         }

         private void buttonP1set_Click(object sender, EventArgs e)
         {
             bool result = double.TryParse(textBoxP1.Text, out P1);
             if (result == true)
             {
                 src.WriteString(":SOUR1:PHAS " + P1, true);
                 src.WriteString(":SOUR1:PHAS:SYNC ", true);
             }
             else
             {
                 MessageBox.Show("Error: Input type double Phase (deg).");
                 return;
             }
                 

         }

         private void buttonP2get_Click(object sender, EventArgs e)
         {

         }

         private void buttonP2set_Click(object sender, EventArgs e)
         {
             bool result = double.TryParse(textBoxP2.Text, out P2);
             if (result == true)
             {
                 src.WriteString(":SOUR2:PHAS " + P2, true);
                 src.WriteString(":SOUR2:PHAS:SYNC ", true);
             }
             else
             {
                 MessageBox.Show("Error: Input type double Phase (deg).");
                 return;
             }
                 
         }

         private void buttonOut1_Click(object sender, EventArgs e) 
         {
             if (out1 == 1)
             {
                 src.WriteString("OUTP1:STAT " + 0, true);
                 out1 = 0;
             }
             else if (out1 == 0)
             {
                 src.WriteString("OUTP1:STAT " + 1, true);
                 out1 = 1;
             }
             else
                 MessageBox.Show("ERROR: Failed to adjust device settings.");

             UpdateButtons(out1, out2);
             
         }

         private void buttonOut2_Click(object sender, EventArgs e) 
         {

             if (out2 == 1)
             {
                 src.WriteString("OUTP2:STAT " + 0, true);
                 out2 = 0;
             }
             else if (out2 == 0)
             {
                 src.WriteString("OUTP2:STAT " + 1, true);
                 out2 = 1;
             }
             else
                 MessageBox.Show("ERROR: Failed to adjust device settings.");

             UpdateButtons(out1, out2);
             
         }
        #endregion AgilentFG

         private void tabPage2_Click(object sender, EventArgs e)
         {

         }

         #region Serialport
         private void btnSerialporttest_Click(object sender, EventArgs e)
         {
             //SerialLockin = new SerialPort();

             string[] ArrayComPortsNames = null;
             int index = -1;
             string ComPortName = null;
             rtbIncoming.Text = "";
             cboPorts.Items.Clear();
             ArrayComPortsNames = SerialPort.GetPortNames();
             do
             {
                 index += 1;
                 rtbIncoming.Text += ArrayComPortsNames[index] + "\n";
             }
             while (!((ArrayComPortsNames[index] == ComPortName) ||
                                 (index == ArrayComPortsNames.GetUpperBound(0))));
             index = -1;
            
             do
             {
                 index += 1;
                 cboPorts.Items.Add(ArrayComPortsNames[index]);
             }

             while (!((ArrayComPortsNames[index] == ComPortName)
                           || (index == ArrayComPortsNames.GetUpperBound(0))));
             Array.Sort(ArrayComPortsNames);

             //want to get first out
             if (index == ArrayComPortsNames.GetUpperBound(0))
             {
                 ComPortName = ArrayComPortsNames[0];
             }
             cboPorts.Text = ArrayComPortsNames[0];

         }

         private void btnOpenLockin_Click(object sender, EventArgs e)
         {
             if(btnOpenLockin.Text=="OpenLockin")
             {
                 
                // SerialLockin.PortName = Convert.ToString(cboPorts.Text);

                 if (!(SerialLockin.IsOpen))
                 {
                     SerialLockin.PortName = "COM7";
                     SerialLockin.BaudRate = 9600;
                     SerialLockin.Parity = Parity.None;
                     SerialLockin.StopBits = StopBits.One;
                     SerialLockin.DataBits = 8;
                     SerialLockin.Handshake = Handshake.None;
                     SerialLockin.DataReceived += new SerialDataReceivedEventHandler(DataReceivedHandler);
                     SerialLockin.Open();
                     SerialLockin.RtsEnable = true;
                     SerialLockin.DtrEnable = true;
                     btnOpenLockin.Text = "CloseLockin";
                     btnOpenLockin.BackColor=Color.Green;
                 }
             }
             else
             {
                 SerialLockin.Close();
                 btnOpenLockin.Text = "OpenLockin";
                 btnOpenLockin.BackColor = Color.Gray;
             }
         }

         private void btnReadxy_Click(object sender, EventArgs e)
         {
             SerialLockin.Write("SNAP? 1,2 \r");
            
         }


         private void DataReceivedHandler(
                         object sender,
                         SerialDataReceivedEventArgs e)
         {
             SerialPort sp = (SerialPort)sender;
             string indata = sp.ReadExisting();
            // System.IO.File.WriteAllText(@"d:\data\WriteText.txt", indata);

             /*
             System.IO.File.AppendAllText(
                            @"d:\data\WriteText.txt",
                            indata + Environment.NewLine
                            );
              * */
            

             serindata = indata;
             lockinreaddata = indata;
         }
         #endregion Serialport
         private void btnWritefile_Click(object sender, EventArgs e)
         {
             string text = "A class is the most powerful data type in C#. Like a structure, " +
               "a class defines the data and behavior of the data type. ";
             // WriteAllText creates a file, writes the specified string to the file, 
             // and then closes the file.
             System.IO.File.AppendAllText(
                          @"d:\data\WriteText.txt",
                          text + Environment.NewLine
                          );

         }

         private void btnOpenMcu_Click(object sender, EventArgs e)
         {
              // SerialLockin.PortName = Convert.ToString(cboPorts.Text);

             if (!(SerialMcu.IsOpen))
                 {
                     SerialMcu.PortName = Convert.ToString(cboPorts.Text);
                     //SerialLockin.PortName = "COM7";
                     /*
                     SerialMcu.BaudRate = 9600;
                     SerialLockin.Parity = Parity.None;
                     SerialLockin.StopBits = StopBits.One;
                     SerialLockin.DataBits = 8;
                     SerialLockin.Handshake = Handshake.None;
                       */
                     SerialMcu.DataReceived += new SerialDataReceivedEventHandler(DataReceivedHandlerMcu);
                     SerialMcu.Open();
                     SerialMcu.RtsEnable = true;
                     SerialMcu.DtrEnable = true;
                     btnOpenMcu.Text = "CloseMcu";
                     btnOpenMcu.BackColor = Color.Green;
                 }
             
             else
             {
                 SerialMcu.Close();
                 btnOpenMcu.Text = "OpenMcu";
                 btnOpenMcu.BackColor = Color.Gray;
             }
         }

         private void DataReceivedHandlerMcu(
                          object sender,
                          SerialDataReceivedEventArgs e)
         {
             SerialPort sp = (SerialPort)sender;
             string indata = sp.ReadExisting();
             // System.IO.File.WriteAllText(@"d:\data\WriteText.txt", indata);
            /*
             System.IO.File.AppendAllText(
                            @"d:\data\WriteText.txt",
                            indata + Environment.NewLine
                            );
             * */
             serindata = indata;
          
         }

         private void btnWriteMux_Click(object sender, EventArgs e)
         {
             
             byte num ;
             
             num= System.Convert.ToByte(rtbSendData.Text);

             writemux(num);
         }

         private void writemux(byte mux)
         {
             string sout = null;
             byte[] num = new byte[4];
             num[0] = 66;
             num[1] = 65;
             num[2] = 76;
             num[3] = mux;

             sout = System.Text.Encoding.Default.GetString(num);

             SerialMcu.Write(sout);
         }

         private void buttonR_Click(object sender, EventArgs e)
         {

         }

         private void buttonL_Click(object sender, EventArgs e)
         {

         }

         private void btnSeriaCheck_Click(object sender, EventArgs e)
         {
             rtbIncoming.Text = serindata;
         }
         #region work
         private void preparetowork(bool mcu, bool wavegenerator)
         {
             //open lockin
             if (btnOpenLockin.Text == "OpenLockin")
             {

                 // SerialLockin.PortName = Convert.ToString(cboPorts.Text);

                 if (!(SerialLockin.IsOpen))
                 {
                     SerialLockin.PortName = "COM7";
                     SerialLockin.BaudRate = 9600;
                     SerialLockin.Parity = Parity.None;
                     SerialLockin.StopBits = StopBits.One;
                     SerialLockin.DataBits = 8;
                     SerialLockin.Handshake = Handshake.None;
                     SerialLockin.DataReceived += new SerialDataReceivedEventHandler(DataReceivedHandler);
                     SerialLockin.Open();
                     SerialLockin.RtsEnable = true;
                     SerialLockin.DtrEnable = true;
                     btnOpenLockin.Text = "CloseLockin";
                     btnOpenLockin.BackColor = Color.Green;
                 }
             }
             else
             {

             }
             
             if (mcu)
             {                
                 //open mux controller
                 if (!(SerialMcu.IsOpen))
                 {
                     SerialMcu.PortName = "COM5";
                     //SerialLockin.PortName = "COM7";
                     /*
                     SerialMcu.BaudRate = 9600;
                     SerialLockin.Parity = Parity.None;
                     SerialLockin.StopBits = StopBits.One;
                     SerialLockin.DataBits = 8;
                     SerialLockin.Handshake = Handshake.None;
                       */
                     SerialMcu.DataReceived += new SerialDataReceivedEventHandler(DataReceivedHandlerMcu);
                     SerialMcu.Open();
                     SerialMcu.RtsEnable = true;
                     SerialMcu.DtrEnable = true;
                     btnOpenMcu.Text = "CloseMcu";
                     btnOpenMcu.BackColor = Color.Green;
                 }

                 else
                 {

                 }
             }
             

             //connect scanner
             try
             {
                 // Connect to A3200 controller.  
                 this.myController = Controller.Connect();
                 checkControllerConnected.Checked = true;
                 EnableControls(true); //call first function: EnableControls to enable all CNC control stuff 

                 // populate axis names (not used in new layout -- don't delete in case additional functionality required)
                 foreach (AxisInfo axis in this.myController.Information.Axes)
                 {
                     comboAxis.Items.Add(axis.Name);
                 }
                 this.axisIndex = 0;
                 comboAxis.SelectedIndex = this.axisIndex;

                 // populate task names (unused)
                 foreach (Aerotech.A3200.Tasks.Task task in this.myController.Tasks)
                 {
                     if (task.State != TaskState.Inactive)
                     {
                         comboTask.Items.Add(task.Name.ToString());
                     }
                 }
                 // Task 0 is reserved
                 this.taskIndex = 1;
                 comboTask.SelectedIndex = this.taskIndex - 1;

                 // register task state and diagPackect arrived events
                 this.myController.ControlCenter.TaskStates.NewTaskStatesArrived += new EventHandler<NewTaskStatesArrivedEventArgs>(TaskStates_NewTaskStatesArrived);
                 this.myController.ControlCenter.Diagnostics.NewDiagPacketArrived += new EventHandler<NewDiagPacketArrivedEventArgs>(Diagnostics_NewDiagPacketArrived);
             }
             catch (A3200Exception exception)
             {
                 labelErrorMessage.Text = exception.Message;
             }

             //--------------------
             //disable and enable axis
             try
             {
                 this.myController.Commands[this.taskIndex].Axes["X"].Motion.Disable();
             }
             catch (A3200Exception exception)
             {
                 labelErrorMessage.Text = exception.Message;
             }

             try
             {
                 this.myController.Commands[this.taskIndex].Axes["X"].Motion.Enable();
             }
             catch (A3200Exception exception)
             {
                 labelErrorMessage.Text = exception.Message;
             }

             try
             {
                 this.myController.Commands[this.taskIndex].Axes["Y"].Motion.Disable();
             }
             catch (A3200Exception exception)
             {
                 labelErrorMessage.Text = exception.Message;
             }

             try
             {
                 this.myController.Commands[this.taskIndex].Axes["Y"].Motion.Enable();
             }
             catch (A3200Exception exception)
             {
                 labelErrorMessage.Text = exception.Message;
             }

             try
             {
                 this.myController.Commands[this.taskIndex].Axes["Z"].Motion.Disable();
             }
             catch (A3200Exception exception)
             {
                 labelErrorMessage.Text = exception.Message;
             }

             try
             {
                 this.myController.Commands[this.taskIndex].Axes["Z"].Motion.Enable();
             }
             catch (A3200Exception exception)
             {
                 labelErrorMessage.Text = exception.Message;
             }

             if (wavegenerator)
             {
                 //----------------------
                 //
                 // connect wave generater
                 string srcAddress = textBoxFGaddress.Text;
                 src.IO = (IMessage)rMgr.Open(srcAddress, AccessMode.NO_LOCK, 2000, null);
                 src.IO.Timeout = 3000;

                 checkFGConnected.Checked = true;

                 //Synchronizes phases (always desired ?)
                 src.WriteString(":SOUR2:PHAS:SYNC ", true);

                 //Updates all textboxes with current values
                 src.WriteString(":SOUR1:FREQ? ", true);
                 textBoxF1.Text = src.ReadString();
                 src.WriteString(":SOUR2:FREQ? ", true);
                 textBoxF2.Text = src.ReadString();
                 src.WriteString(":SOUR1:VOLT? ", true);
                 textBoxA1.Text = src.ReadString();
                 src.WriteString(":SOUR2:VOLT? ", true);
                 textBoxA2.Text = src.ReadString();
                 src.WriteString(":SOUR1:PHAS? ", true);
                 textBoxP1.Text = src.ReadString();

                 src.WriteString(":SOUR2:PHAS? ", true);

                 textBoxP2.Text = src.ReadString();

                 //Updates buttons
                 src.WriteString(":OUTP1:STAT? ", true);
                 out1 = int.Parse(src.ReadString());
                 src.WriteString(":OUTP2:STAT? ", true);
                 out2 = int.Parse(src.ReadString());
                 UpdateButtons(out1, out2); //See #region MoarFunc
             }
     
             

         }

         private void openFileDialog1_FileOk(object sender, CancelEventArgs e)
         {

         }

         private void run1line(int nfre, string[] fre, int nsensor, double pauseTime, string filename)
         {
             DateTime Tthen = DateTime.Now; //Tthen = current time
            

             int i, j;
             string lockindata="";
             double f;
             for (i = 0; i < nfre ; i++)
             {
                 lockindata = "";
                 f = Convert.ToDouble(fre[i]);
                 setfrquency(f,f);
                 for (j = 0; j < nsensor ; j++)
                 {
                     if(nsensor>1)
                     {
                     writemux(Convert.ToByte(j));
                     }
                     
                     TimeWait(pauseTime);
                     SerialLockin.Write("SNAP? 1,2 \r");
                     Tthen = DateTime.Now;
                     while (String.IsNullOrEmpty(lockinreaddata))
                     {
                         if (Tthen.AddSeconds(5) < DateTime.Now)      //if there is not response from the lockin amplifier in 2 seconds
                         {
                             MessageBox.Show("there is not response from lockin amplifier");
                             break;
                         }
                     }
                     if (!String.IsNullOrEmpty(lockinreaddata))
                     {
                         lockindata += lockinreaddata;
                         lockinreaddata = "";
                     }

                 }

                 string fn = filename+Convert.ToString(fre[i])+"Hz.txt";
                 System.IO.File.AppendAllText(
                           fn,lockindata + Environment.NewLine
                           );
             }
         }

         private void run1point(int nfre, string[] fre, double pauseTime, string filename)
         {
             DateTime Tthen = DateTime.Now; //Tthen = current time


             int i, j;
             string lockindata = "";
             double f;
             for (i = 0; i < nfre; i++)
             {
                 lockindata = "";
                 f = Convert.ToDouble(fre[i]);
                 setfrquency(f, f);
               
                     TimeWait(pauseTime);
                     SerialLockin.Write("SNAP? 1,2 \r");
                     Tthen = DateTime.Now;
                     while (String.IsNullOrEmpty(lockinreaddata))
                     {
                         if (Tthen.AddSeconds(5) < DateTime.Now)      //if there is not response from the lockin amplifier in 2 seconds
                         {
                             MessageBox.Show("there is not response from lockin amplifier");
                             break;
                         }
                     }
                     if (!String.IsNullOrEmpty(lockinreaddata))
                     {
                         lockindata += lockinreaddata;
                         lockinreaddata = "";
                     }

                 string fn = filename + Convert.ToString(fre[i]) + "Hz.txt";
                 System.IO.File.AppendAllText(
                           fn, lockindata + Environment.NewLine
                           );
                 /*
                 double dcvalue=fADCmeasureDCvalue(400, 100*Convert.ToInt32(f));
                 string sdcvalue = Convert.ToString(dcvalue);
                 fn = filename + Convert.ToString(fre[i]) + "HzDC.txt";
                 System.IO.File.AppendAllText(
                           fn, sdcvalue + Environment.NewLine
                           );*/
             }
         }
#endregion work;

         private void btnTest_Click(object sender, EventArgs e)
         {
             string filename = rtbFilefoder.Text;
             double [,] testdata=new double [6,2];
             for (int i = 0; i < 6; i++)
             {
                 testdata[i,0] = i + 0.1;
                 testdata[i,1] = i + 0.2;
             }
             double d = 1.235689;
             string sd = d.ToString();


             using (StreamWriter writer = new StreamWriter(filename,true))
             {
                 foreach (var value in testdata)
                 {
                     writer.WriteLine(value);
                 }
             }
         }

         private void btnMoveX_Click(object sender, EventArgs e)
         {
             double movedistance = Convert.ToDouble(rtbMovedistance.Text);
             string axis = "X";
             try
             {
                 myController.Commands.Motion.Linear(axis, movedistance);
             }
             catch (A3200Exception exception)
             {
                 labelErrorMessage.Text = exception.Message + "Here is a helplink: " + exception.HelpLink;
                 MessageBox.Show("Houston, we have a problem.  Your scanner dun fucked up."); //CHANGE BEFORE SUBMISSION                 
             }
         }

         private void btnMoveY_Click(object sender, EventArgs e)
         {
             double movedistance = Convert.ToDouble(rtbMovedistance.Text);
             string axis = "Y";
             try
             {
                 myController.Commands.Motion.Linear(axis, movedistance);
             }
             catch (A3200Exception exception)
             {
                 labelErrorMessage.Text = exception.Message + "Here is a helplink: " + exception.HelpLink;
                 MessageBox.Show("Houston, we have a problem.  Your scanner dun fucked up."); //CHANGE BEFORE SUBMISSION                 
             }
         }

         private void btnMoveZ_Click(object sender, EventArgs e)
         {
             double movedistance = Convert.ToDouble(rtbMovedistance.Text);
             string axis = "Z";
             try
             {
                 myController.Commands.Motion.Linear(axis, movedistance);
             }
             catch (A3200Exception exception)
             {
                 labelErrorMessage.Text = exception.Message + "Here is a helplink: " + exception.HelpLink;
                 MessageBox.Show("Houston, we have a problem.  Your scanner dun fucked up."); //CHANGE BEFORE SUBMISSION                 
             }
         }

         private void textBoxF1_TextChanged(object sender, EventArgs e)
         {

         }

         private void textBoxF2_TextChanged(object sender, EventArgs e)
         {

         }

         private void label40_Click(object sender, EventArgs e)
         {

         }

         private void tabPageCScan_Click(object sender, EventArgs e)
         {

         }

         private void button2_Click(object sender, EventArgs e)
         {
             preparetowork(true,true);
             rungridscan();
         }

         private void rungridscan()
         {

             double stepSize = 0; //Size of step to be iterated through
             //int flag = 0; //Tracks user input, where 1 = input distance, 2 = input step size 

             double pauseTime = Convert.ToDouble(textBoxPausetime.Text);
             string filename = rtbFilefoder.Text;
             string[] sfre = rtbFrequencies.Text.Split(',');
             int nfre = sfre.Length;
             int nx = Convert.ToInt16(textBoxStepnumberX.Text);
             int ny = Convert.ToInt16(textBoxStepnumberY.Text);

             double stepx = Convert.ToDouble(textBoxStepsizeX.Text);

             double stepy = Convert.ToDouble(textBoxStepsizeY.Text);
             int numDatapoints = nx;

             //After movement has been determined, check to ensure the E-Stop hasn't been pressed. (Saftey feature!!)
             if (globalFlag == 1)
                 return;


             //Pause and move
             //bool completedtheloop = false;
             //test scanner

             /*
             axisLetter = "Y";
             stepSize = stepy;
             try
             {
                 this.myController.Commands.Motion.Linear(axisLetter, stepSize);
             }
             catch (A3200Exception exception)
             {
                 labelErrorMessage.Text = exception.Message + "Here is a helplink: " + exception.HelpLink;
                 MessageBox.Show("Houston, we have a problem.  Your scanner dun fucked up."); //CHANGE BEFORE SUBMISSION                
             }

             try
             {
                 this.myController.Commands.Motion.Linear(axisLetter, -stepSize);
             }
             catch (A3200Exception exception)
             {
                 labelErrorMessage.Text = exception.Message + "Here is a helplink: " + exception.HelpLink;
                 MessageBox.Show("Houston, we have a problem.  Your scanner dun fucked up."); //CHANGE BEFORE SUBMISSION                
             }
              * */

             //get data
             for (int iy = 0; iy < ny; iy++)
             {
                 axisLetter = "X";
                 stepSize = stepx;
                 writemux(Convert.ToByte(15));
                // writemux(Convert.ToByte(31));   //for sensor test only
                 run1point(nfre, sfre, pauseTime, filename);

                 for (int i = 1; i < numDatapoints; i++)
                 {

                     //writemux(Convert.ToByte(31-i));   //for sensor test only

                     TimeWait(pauseTime); //Calls function to delay without pausing thread execution like sleep(n) does

                     //Right before movement, again make sure E-Stop hasn't been pressed
                     if (globalFlag == 1)
                         return;

                     //Move to next point in x direction

                     try
                     {
                         this.myController.Commands.Motion.Linear(axisLetter, stepSize);
                     }
                     catch (A3200Exception exception)
                     {
                         labelErrorMessage.Text = exception.Message + "Here is a helplink: " + exception.HelpLink;
                         MessageBox.Show("Houston, we have a problem.  Your scanner dun fucked up."); //CHANGE BEFORE SUBMISSION
                         break;
                     }
                     run1point(nfre, sfre, pauseTime, filename);

                 }

                 //move back to initial position in x direction
                 //Move to next point
                 try
                 {
                     this.myController.Commands.Motion.Linear(axisLetter, -(numDatapoints - 1) * stepSize);
                 }
                 catch (A3200Exception exception)
                 {
                     labelErrorMessage.Text = exception.Message + "Here is a helplink: " + exception.HelpLink;
                     MessageBox.Show("Houston, we have a problem.  Your scanner dun fucked up."); //CHANGE BEFORE SUBMISSION                 
                 }

                 //move along y direction
                 axisLetter = "Y";
                 stepSize = stepy;
                 try
                 {
                     this.myController.Commands.Motion.Linear(axisLetter, stepSize);
                 }
                 catch (A3200Exception exception)
                 {
                     labelErrorMessage.Text = exception.Message + "Here is a helplink: " + exception.HelpLink;
                     MessageBox.Show("Houston, we have a problem.  Your scanner dun fucked up."); //CHANGE BEFORE SUBMISSION                
                 }
             }

             axisLetter = "Y";
             stepSize = stepy;
             try
             {
                 this.myController.Commands.Motion.Linear(axisLetter, -(ny) * stepy);
             }
             catch (A3200Exception exception)
             {
                 labelErrorMessage.Text = exception.Message + "Here is a helplink: " + exception.HelpLink;
                 MessageBox.Show("Houston, we have a problem.  Your scanner dun fucked up."); //CHANGE BEFORE SUBMISSION                
             }

         }

         private void buttonTestFG2_Click(object sender, EventArgs e)
         {
             string srcAddress = textBoxFG33220address.Text;
             FWG33220.IO = (IMessage)rMgr.Open(srcAddress, AccessMode.NO_LOCK, 2000, null);
             FWG33220.IO.Timeout = 3000;

             FWG33220.WriteString("*rst", true);

             FWG33220.IO.Clear();
             FWG33220.WriteString("FUNCtion SINusoid", true);
             FWG33220.WriteString("OUTput:LOAD 50", true);
             FWG33220.WriteString("FREQuency 1000", true);
             FWG33220.WriteString("VOLTage 1.2", true);
             FWG33220.WriteString("VOLTage:OFFSet 0", true);
             FWG33220.WriteString("OUTPut ON", true);
         }

         private double[,] DAQ_Capture(int samples, int clock, string channel1, string channel2, string channel3, double lowerlim, double upperlim)
         {
             NationalInstruments.DAQmx.Task analogInTask = new NationalInstruments.DAQmx.Task(); //initializes task object

             AIChannel myAIChannel1; //initilizes channel

             myAIChannel1 = analogInTask.AIChannels.CreateVoltageChannel(
             channel1,
             "myAIChannel1",
             AITerminalConfiguration.Differential,
             lowerlim,
             upperlim,
             AIVoltageUnits.Volts
             ); //creates voltage channel

             AIChannel myAIChannel2; //initilizes channel

             myAIChannel2 = analogInTask.AIChannels.CreateVoltageChannel(
             channel2,
             "myAIChannel2",
             AITerminalConfiguration.Differential,
             lowerlim,
             upperlim,
             AIVoltageUnits.Volts
             ); //creates voltage channel


             AIChannel myAIChannel3; //initilizes channel

             myAIChannel3 = analogInTask.AIChannels.CreateVoltageChannel(
             channel3,
             "myAIChannel3",
             AITerminalConfiguration.Differential,
             lowerlim,
             upperlim,
             AIVoltageUnits.Volts
             ); //creates voltage channel

            
             analogInTask.Timing.ConfigureSampleClock("", clock, SampleClockActiveEdge.Rising, SampleQuantityMode.FiniteSamples, samples); //sets clock
             
             AnalogMultiChannelReader reader = new AnalogMultiChannelReader(analogInTask.Stream); //creates reading stream

             double[,] data = reader.ReadMultiSample(samples); //populates data
             return data;

             /*
             string filename = richTextBoxADCFileLocation.Text;
             using (System.IO.StreamWriter file = new System.IO.StreamWriter(filename)) //writes data to filepath
             {
                 foreach (double line in data)
                 {
                     file.WriteLine(line);
                 }

             }
              * */



         }

         private double fADCmeasureDCvalue(int naverage, int samplefre)
         {
             int samples = naverage;
             int clock = samplefre;   //Hz
             string channel1 = "dev1/ai3";
             string channel2 = "dev1/ai2";
             string channel3 = "dev1/ai4";
             double lowerlim = -5;
             double upperlim = 5;

             double[,] data = DAQ_Capture(samples, clock, channel1, channel2, channel3, lowerlim, upperlim);
             int i = 0;
             double average = data[1, 0];
             for (i = 1; i < samples; i++)
             {
                 average = average + data[1, i];
             }
             average = average / samples;
             return average;
         }
         private void buttonADQtest_Click(object sender, EventArgs e)
         {
             int samples = Convert.ToInt32(textBoxADCsamples.Text);
             int clock = Convert.ToInt32(textBoxADCFrequency.Text);   //Hz
             string channel1 = "dev1/ai3";
             string channel2 = "dev1/ai2";
             string channel3 = "dev1/ai4";
             double lowerlim = -5;
             double upperlim = 5;

             double[,] data=DAQ_Capture(samples, clock, channel1, channel2,channel3,lowerlim, upperlim);
             string filename = richTextBoxADCFileLocation.Text;
             using (System.IO.StreamWriter file = new System.IO.StreamWriter(filename)) //writes data to filepath
             {
                 foreach (double line in data)
                 {
                     file.WriteLine(line);
                 }

             }
            
         }

         private void linescanwithlockin(double Xdist, double Ydist, double Zdist, double Xstep, double Ystep, double Zstep, int numDatapoints, double pauseTime) //Main movement function.  Executes when buttonExe is clicked.
         {
             double stepSize = 0; //Size of step to be iterated through
             int flag = 0; //Tracks user input, where 1 = input distance, 2 = input step size 

             string filename = rtbFilefoder.Text;
             string[] sfre = rtbFrequencies.Text.Split(',');
             int nfre = sfre.Length;
             int nsensor = Convert.ToInt16(rtbNumberofsensors.Text);



             if ((Xstep != 0) || (Ystep != 0) || (Zstep != 0)) //if the user is inputting a step size and number of datapoints
             {
                 if (Xstep != 0)
                 {
                     axisLetter = "X";
                     stepSize = Xstep;
                 }
                 else if (Ystep != 0)
                 {
                     axisLetter = "Y";
                     stepSize = Ystep;
                 }
                 else if (Zstep != 0)
                 {
                     axisLetter = "Z";
                     stepSize = Zstep;
                 }
                 else
                 {
                     MessageBox.Show("Congrats, you somehow managed to completely screw this program, narrowly missing else if statements and catches.  You should feel proud of yourself.  Unfortunately execution has been halted.");
                     return;
                 }


             }
             //After movement has been determined, check to ensure the E-Stop hasn't been pressed. (Saftey feature!!)
             if (globalFlag == 1)
                 return;


             //Pause and move
             //bool completedtheloop = false;
             //test scanner
             try
             {
                 this.myController.Commands.Motion.Linear(axisLetter, stepSize);
             }
             catch (A3200Exception exception)
             {
                 labelErrorMessage.Text = exception.Message + "Here is a helplink: " + exception.HelpLink;
                 MessageBox.Show("Houston, we have a problem.  Your scanner dun fucked up."); //CHANGE BEFORE SUBMISSION                
             }

             try
             {
                 this.myController.Commands.Motion.Linear(axisLetter, -stepSize);
             }
             catch (A3200Exception exception)
             {
                 labelErrorMessage.Text = exception.Message + "Here is a helplink: " + exception.HelpLink;
                 MessageBox.Show("Houston, we have a problem.  Your scanner dun fucked up."); //CHANGE BEFORE SUBMISSION                
             }

             //get data

             run1linewithlockin(nfre, sfre, nsensor, pauseTime, filename);
             for (int i = 1; i < numDatapoints; i++)
             {



                 TimeWait(pauseTime); //Calls function to delay without pausing thread execution like sleep(n) does

                 //Right before movement, again make sure E-Stop hasn't been pressed
                 if (globalFlag == 1)
                     return;

                 //Move to next point
                 try
                 {
                     this.myController.Commands.Motion.Linear(axisLetter, stepSize);
                 }
                 catch (A3200Exception exception)
                 {
                     labelErrorMessage.Text = exception.Message + "Here is a helplink: " + exception.HelpLink;
                     MessageBox.Show("Houston, we have a problem.  Your scanner dun fucked up."); //CHANGE BEFORE SUBMISSION
                     break;
                 }
                 run1linewithlockin(nfre, sfre, nsensor, pauseTime, filename);

             }

             //move back to initial position
             //Move to next point
             try
             {
                 this.myController.Commands.Motion.Linear(axisLetter, -(numDatapoints - 1) * stepSize);
             }
             catch (A3200Exception exception)
             {
                 labelErrorMessage.Text = exception.Message + "Here is a helplink: " + exception.HelpLink;
                 MessageBox.Show("Houston, we have a problem.  Your scanner dun fucked up."); //CHANGE BEFORE SUBMISSION                 
             }


         }

         private void run1linewithlockin(int nfre, string[] fre, int nsensor, double pauseTime, string filename)
         {
             DateTime Tthen = DateTime.Now; //Tthen = current time


             int i, j;
             string lockindata = "";
             double f;
             double dcvalue;
             string sdcvalue;

             for (i = 0; i < nfre; i++)
             {
                 lockindata = "";
                 f = Convert.ToDouble(fre[i]);
                 setfrquency(f, f);

                 //real component
                 for (j = 0; j < nsensor; j++)
                 {
                     writemux(Convert.ToByte(j));
                     TimeWait(pauseTime);
                     dcvalue = fADCmeasureDCvalue(400, 100 * Convert.ToInt32(f));
                     sdcvalue = Convert.ToString(dcvalue);
                     lockindata += sdcvalue;
                     lockindata += "\n";

                 }

                 string fn = filename + Convert.ToString(fre[i]) + "Hzre.txt";
                 System.IO.File.AppendAllText(
                           fn, lockindata + Environment.NewLine
                           );

                 //imaginary component
                 lockindata = "";
                 for (j = 96; j < 96+nsensor; j++)
                 {
                     writemux(Convert.ToByte(j));
                     TimeWait(pauseTime);
                     dcvalue = fADCmeasureDCvalue(400, 100 * Convert.ToInt32(f));
                     sdcvalue = Convert.ToString(dcvalue);
                     lockindata += sdcvalue;
                     lockindata += "\n";

                 }

                 fn = filename + Convert.ToString(fre[i]) + "Hzim.txt";
                 System.IO.File.AppendAllText(
                           fn, lockindata + Environment.NewLine
                           );
             }
         }


         private void Linescanwithlockin_Click(object sender, EventArgs e)
         {
             preparetowork(true,true);
             linescanwithlockin(double.Parse(textBoxXdist.Text), double.Parse(textBoxYdist.Text), double.Parse(textBoxZdist.Text),
                 double.Parse(textBoxXstep.Text), double.Parse(textBoxYstep.Text), double.Parse(textBoxZstep.Text),
                 int.Parse(textBoxDatapoints.Text), double.Parse(textBoxPausetime.Text)); 
         }

         private void Velocitytest_Click(object sender, EventArgs e)
         {
             preparetowork(true,true);
             rungridscanvelocitytest();
         }

         private void rungridscanvelocitytest()
         {

             double stepSize = 0; //Size of step to be iterated through
             //int flag = 0; //Tracks user input, where 1 = input distance, 2 = input step size 

             //double pauseTime = Convert.ToDouble(textBoxPausetime.Text);

             string initfilename = rtbFilefoder.Text;

             string[] sallfre = rtbFrequencies.Text.Split(',');
             int ntotalfre = sallfre.Length;

             string[] spausetime = textBoxPausetime.Text.Split(',');
             int npt = spausetime.Length;

             int nx = Convert.ToInt16(textBoxStepnumberX.Text);
             int ny = Convert.ToInt16(textBoxStepnumberY.Text);

             double stepx = Convert.ToDouble(textBoxStepsizeX.Text);

             double stepy = Convert.ToDouble(textBoxStepsizeY.Text);
             int numDatapoints = nx;

             //After movement has been determined, check to ensure the E-Stop hasn't been pressed. (Saftey feature!!)
             if (globalFlag == 1)
                 return;

             string[] sfre = sallfre;
             double pauseTime;
             string filename;


             int nfre = 1;
             for (int ncurrentfre = 0; ncurrentfre < ntotalfre;ncurrentfre++ )
             {
                 for (int ncurrentpause = 0; ncurrentpause < npt; ncurrentpause++)
                 {
                     sfre[0] = sallfre[ncurrentfre];
                     pauseTime = Convert.ToDouble(spausetime[ncurrentpause]);

                     filename = initfilename + Convert.ToString(pauseTime*1000) ;
                     //get data
                     for (int iy = 0; iy < ny; iy++)
                     {
                         axisLetter = "X";
                         stepSize = stepx;
                         //writemux(Convert.ToByte(15));
                       
                         run1point(nfre, sfre, pauseTime, filename);

                         for (int i = 1; i < numDatapoints; i++)
                         {

                             //writemux(Convert.ToByte(31-i));   //for sensor test only

                             TimeWait(pauseTime); //Calls function to delay without pausing thread execution like sleep(n) does

                             //Right before movement, again make sure E-Stop hasn't been pressed
                             if (globalFlag == 1)
                                 return;

                             //Move to next point in x direction

                             try
                             {
                                 this.myController.Commands.Motion.Linear(axisLetter, stepSize);
                             }
                             catch (A3200Exception exception)
                             {
                                 labelErrorMessage.Text = exception.Message + "Here is a helplink: " + exception.HelpLink;
                                 MessageBox.Show("Houston, we have a problem.  Your scanner dun fucked up."); //CHANGE BEFORE SUBMISSION
                                 break;
                             }
                             run1point(nfre, sfre, pauseTime, filename);

                         }

                         //move back to initial position in x direction
                         //Move to next point
                         try
                         {
                             this.myController.Commands.Motion.Linear(axisLetter, -(numDatapoints - 1) * stepSize);
                         }
                         catch (A3200Exception exception)
                         {
                             labelErrorMessage.Text = exception.Message + "Here is a helplink: " + exception.HelpLink;
                             MessageBox.Show("Houston, we have a problem.  Your scanner dun fucked up."); //CHANGE BEFORE SUBMISSION                 
                         }

                         //move along y direction
                         axisLetter = "Y";
                         stepSize = stepy;
                         try
                         {
                             this.myController.Commands.Motion.Linear(axisLetter, stepSize);
                         }
                         catch (A3200Exception exception)
                         {
                             labelErrorMessage.Text = exception.Message + "Here is a helplink: " + exception.HelpLink;
                             MessageBox.Show("Houston, we have a problem.  Your scanner dun fucked up."); //CHANGE BEFORE SUBMISSION                
                         }
                     }

                     axisLetter = "Y";
                     stepSize = stepy;
                     try
                     {
                         this.myController.Commands.Motion.Linear(axisLetter, -(ny) * stepy);
                     }
                     catch (A3200Exception exception)
                     {
                         labelErrorMessage.Text = exception.Message + "Here is a helplink: " + exception.HelpLink;
                         MessageBox.Show("Houston, we have a problem.  Your scanner dun fucked up."); //CHANGE BEFORE SUBMISSION                
                     }

                 }

             }

                

         }

         private void continuemove1line(string direction, double distance, double speed, int samplefre,string filename)
         {
             double time = Math.Abs(distance / speed);
             int nsamples = (int)Math.Abs(time * samplefre);
             int[] argu = new int[2];
             argu[0] = nsamples;
             argu[1] = samplefre;
             this.flagadcrun = true;
             backgroundWorker1.RunWorkerAsync(argu);
             Application.DoEvents();
         
               string axis = direction;
               try
               {
                   myController.Commands.Motion.Linear(axis, distance, speed);
               }
               catch (A3200Exception exception)
               {
                   labelErrorMessage.Text = exception.Message + "Here is a helplink: " + exception.HelpLink;
                   MessageBox.Show("Houston, we have a problem.  Your scanner dun fucked up."); //CHANGE BEFORE SUBMISSION                 
               }

               try
               {
                   myController.Commands.Motion.Linear(axis, -distance, 50);
               }
               catch (A3200Exception exception)
               {
                   labelErrorMessage.Text = exception.Message + "Here is a helplink: " + exception.HelpLink;
                   MessageBox.Show("Houston, we have a problem.  Your scanner dun fucked up."); //CHANGE BEFORE SUBMISSION                 
               }
               
             //wait for adc to finished
               while (this.flagadcrun)
               { Application.DoEvents(); } //Continue generic operation (stuck in a loop), while }

              int ndata = this.adcresult.GetLength(1);
              //string output="";
            // if (ndata>0)
             //{ output = this.adcresult[0, 0].ToString() + "," + this.adcresult[1, 0].ToString() + Environment.NewLine; }

             /*
             if (ndata > 0)
             {
                 for (int ia = 0; ia < this.adcresult.GetLength(1); ia++)
                 {
                     output =output+ this.adcresult[0, ia].ToString() + "," + this.adcresult[1, ia].ToString() + Environment.NewLine;
                 }
             }
               

               string fn = filename + speed.ToString()+".txt";
            
                   System.IO.File.AppendAllText(
                      fn, output
                      );
              */
              string fn = filename + speed.ToString() + ".txt";
              using (StreamWriter writer = new StreamWriter(fn,true))
              {
                  foreach (var value in this.adcresult)
                  {
                      writer.WriteLine(value);
                  }
              }
         }
         private void Continuemovevt_Click(object sender, EventArgs e)
         {
             string filename = rtbFilefoder.Text;
             string[] sfre = rtbFrequencies.Text.Split(',');
             int nfre = sfre.Length;

            
             int nx = Convert.ToInt16(textBoxStepnumberX.Text);
             int ny = Convert.ToInt16(textBoxStepnumberY.Text);

             double stepx = Convert.ToDouble(textBoxStepsizeX.Text);

             double stepy = Convert.ToDouble(textBoxStepsizeY.Text);

             string direction=textBoxMoveDirection.Text.ToUpper();
             double distance=Convert.ToDouble(textBoxMoveDistance.Text);
             string[] sspeed = Textboxspeedlist.Text.Split(',');
             int nspeed = sspeed.Length;
             double speed=20;
             int samplefre = Convert.ToInt16(textBoxSamplefrequency.Text);
             int noflines = Convert.ToInt16(textBoxNumberoflines.Text);
             double distancebetweenlines = Convert.ToDouble(textBoxDistancebetweenlines.Text);
            

             preparetowork(true, true);

             for (int ispeed = 0; ispeed < nspeed;ispeed++ )
             {
                 speed = Convert.ToDouble(sspeed[ispeed]);
                 #region onespeed
                 for (int ifre = 0; ifre < nfre; ifre++)
                 {
                     double f = Convert.ToDouble(sfre[ifre]);
                     setfrquency(f, f);
                     string lineaxis;
                     if (direction.Equals("Y"))
                     {
                         lineaxis = "X";
                     }
                     else
                     {
                         lineaxis = "Y";
                     }

                     for (int iline = 0; iline < noflines; iline++)
                     {
                         continuemove1line(direction, distance, speed, samplefre, filename + sfre[ifre]);
                        
                             try
                             {
                                 myController.Commands.Motion.Linear(lineaxis, distancebetweenlines, 50);
                             }
                             catch (A3200Exception exception)
                             {
                                 labelErrorMessage.Text = exception.Message + "Here is a helplink: " + exception.HelpLink;
                                 MessageBox.Show(" Your scanner dun fucked up.");
                             }
                        
                     }
                     //move back in lines direction
                     try
                     {
                         myController.Commands.Motion.Linear(lineaxis, -distancebetweenlines * noflines, 50);
                     }
                     catch (A3200Exception exception)
                     {
                         labelErrorMessage.Text = exception.Message + "Here is a helplink: " + exception.HelpLink;
                         MessageBox.Show(" Your scanner dun fucked up.");
                     }

                 }
             #endregion onespeed
              }
         }

         private void backgroundWorker1_DoWork(object sender, DoWorkEventArgs e)
         {
             // Get the BackgroundWorker that raised this event.
             BackgroundWorker worker = sender as BackgroundWorker;

             // Assign the result of the computation
             // to the Result property of the DoWorkEventArgs
             // object. This is will be available to the 
             // RunWorkerCompleted eventhandler.
             int[] argu=(int[])e.Argument;
             e.Result = Adcbackground(argu[0],argu[1], worker, e);
         }

         private void backgroundWorker1_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
         {
             // First, handle the case where an exception was thrown.
             if (e.Error != null)
             {
                 MessageBox.Show(e.Error.Message);
             }
             else if (e.Cancelled)
             {
                 // Next, handle the case where the user canceled 
                 // the operation.
                 // Note that due to a race condition in 
                 // the DoWork event handler, the Cancelled
                 // flag may not have been set, even though
                 // CancelAsync was called.
                
             }
             else
             {
                 // Finally, handle the case where the operation 
                 // succeeded.
                 //resultLabel.Text = e.Result.ToString();
                 //double[,] data = Convert.ToDouble(e.Result); //populates data

                 object src = e.Result;
                 
               double[,] data = (double[,])src;
               this.adcresult = data;

               this.flagadcrun = false;
                /*
                 using (System.IO.StreamWriter file = new System.IO.StreamWriter(@"D:\NIDAQ_results_chan_2.txt")) //writes data to filepath
                 {
                     foreach (double line in data)
                     {
                         file.WriteLine(line);
                     }

                 }*/
             }

             // Enable the UpDown control.
         }

         private double[,] DAQ_RandI(int samples, int clock, string channel1, string channel2, double lowerlim, double upperlim)
         {
             NationalInstruments.DAQmx.Task analogInTask = new NationalInstruments.DAQmx.Task(); //initializes task object

             AIChannel myAIChannel1; //initilizes channel

             myAIChannel1 = analogInTask.AIChannels.CreateVoltageChannel(
             channel1,
             "myAIChannel1",
             AITerminalConfiguration.Differential,
             lowerlim,
             upperlim,
             AIVoltageUnits.Volts
             ); //creates voltage channel

             AIChannel myAIChannel2; //initilizes channel

             myAIChannel2 = analogInTask.AIChannels.CreateVoltageChannel(
             channel2,
             "myAIChannel2",
             AITerminalConfiguration.Differential,
             lowerlim,
             upperlim,
             AIVoltageUnits.Volts
             ); //creates voltage channel


             analogInTask.Timing.ConfigureSampleClock("", clock, SampleClockActiveEdge.Rising, SampleQuantityMode.FiniteSamples, samples); //sets clock

             AnalogMultiChannelReader reader = new AnalogMultiChannelReader(analogInTask.Stream); //creates reading stream

             double[,] data = reader.ReadMultiSample(samples); //populates data
             return data;

         }
         private void backgroundWorker1_ProgressChanged(object sender, ProgressChangedEventArgs e)
         {

         }
         double[,] Adcbackground(int nsample,int nfre, BackgroundWorker worker, DoWorkEventArgs e)
         {
            
             int clock = nfre;   //1 kHz
             string channel1 = "dev1/ai3";
             string channel2 = "dev1/ai2";
             double lowerlim = -10;
             double upperlim = 10;
             double[,] y = new double[2, nsample];
             //double adctime = (double)nsample / nfre;
             int maxns = 4096;
             int realns;
             int nadc = 1;
            
                 if (5 * nfre < maxns)
                 { maxns = 5 * nfre; }

                 if (nsample > maxns)
                 {
                     nadc = nsample / maxns;
                     
                     realns = maxns;
                     if (nsample > nadc * maxns + 2)
                     { nadc = nadc + 1; }
                 }
                 else
                 {
                     realns = nsample;
                     nadc = 1;
                 }

                 int ncurrent;
                 double[,] array1 = DAQ_RandI(realns, clock, channel1, channel2, lowerlim, upperlim);
                 if (nadc == 1)
                 { y = array1; }
                 else
                 {
                     Array.Copy(array1, 0, y, 0, array1.GetLength(1));
                     Array.Copy(array1, array1.GetLength(1), y, nsample, array1.GetLength(1));
                     ncurrent = array1.GetLength(1);
                     for (int iadc = 1; iadc < nadc; iadc++)
                     {
                         if (iadc < nadc - 1)
                         {  }
                         else { realns = nsample - realns * (nadc - 1); }
                         array1 = DAQ_RandI(realns, clock, channel1, channel2, lowerlim, upperlim);
                         Array.Copy(array1, 0, y, ncurrent, array1.GetLength(1));
                         Array.Copy(array1, array1.GetLength(1), y, nsample + ncurrent, array1.GetLength(1));
                         ncurrent = ncurrent + array1.GetLength(1);
                     }
                 }
             
             return y;
         }

         private void button1_Click(object sender, EventArgs e)
         {
             double[,] array1 = new double[2, 5] { { 1, 2,3,4,5 }, { 10,11,12, 13,14 }};

             double[,] array2 = new double[2, 5] { { 1.2, 2.2, 3.2, 4.2,5.2 }, { 10.2, 11.2, 12.2, 13.2,14.2 } };

             double[,] y = new double[2, 10];
             Array.Copy(array1,0,y,0,array1.GetLength(1));
             Array.Copy(array1, array1.GetLength(1), y, 10, array1.GetLength(1));
             int ncurrent = array1.GetLength(1);
             int nsamples = 10;
             Array.Copy(array2, 0, y, ncurrent, array2.GetLength(1));
             Array.Copy(array2, array2.GetLength(1), y, nsamples+ncurrent, array2.GetLength(1));
         }

         private void ButtonContinuemoveArray_Click(object sender, EventArgs e)
         {
             string filename = rtbFilefoder.Text;
             string[] sfre = rtbFrequencies.Text.Split(',');
             int nfre = sfre.Length;


             int nx = Convert.ToInt16(textBoxStepnumberX.Text);
             int ny = Convert.ToInt16(textBoxStepnumberY.Text);

             double stepx = Convert.ToDouble(textBoxStepsizeX.Text);

             double stepy = Convert.ToDouble(textBoxStepsizeY.Text);

             string direction = textBoxMoveDirection.Text.ToUpper();
             double distance = Convert.ToDouble(textBoxMoveDistance.Text);
             string[] sspeed = Textboxspeedlist.Text.Split(',');
             int nspeed = sspeed.Length;
             double speed = 20;
             int samplefre = Convert.ToInt32(textBoxSamplefrequency.Text);
             int noflines = 32;
             double distancebetweenlines = Convert.ToDouble(textBoxDistancebetweenlines.Text);


             preparetowork(true, true);

             for (int ispeed = 0; ispeed < nspeed; ispeed++)
             {
                 speed = Convert.ToDouble(sspeed[ispeed]);
                 #region onespeed
                 for (int ifre = 0; ifre < nfre; ifre++)
                 {
                     double f = Convert.ToDouble(sfre[ifre]);
                     setfrquency(f, f);
                  
                     for (byte iline = 0; iline < noflines; iline++)
                     {
                         writemux(iline);
                         continuemove1line(direction, distance, speed, samplefre, filename + sfre[ifre]);

                     }
                    
                 }
                 #endregion onespeed
             }

         }

         private void Runsinglesensor_Click(object sender, EventArgs e)
         {
             preparetowork(false, true);
             CNCOp(double.Parse(textBoxXdist.Text), double.Parse(textBoxYdist.Text), double.Parse(textBoxZdist.Text),
                 double.Parse(textBoxXstep.Text), double.Parse(textBoxYstep.Text), double.Parse(textBoxZstep.Text),
                 int.Parse(textBoxDatapoints.Text), double.Parse(textBoxPausetime.Text)); 
         }

         private void rungridscanre1mm()
         {
             double returndis = 0.5;
             double stepSize = 0; //Size of step to be iterated through
             //int flag = 0; //Tracks user input, where 1 = input distance, 2 = input step size 

             double pauseTime = Convert.ToDouble(textBoxPausetime.Text);
             string filename = rtbFilefoder.Text;
             string[] sfre = rtbFrequencies.Text.Split(',');
             int nfre = sfre.Length;
             int nx = Convert.ToInt16(textBoxStepnumberX.Text);
             int ny = Convert.ToInt16(textBoxStepnumberY.Text);

             double stepx = Convert.ToDouble(textBoxStepsizeX.Text);

             double stepy = Convert.ToDouble(textBoxStepsizeY.Text);
             int numDatapoints = nx;

             //After movement has been determined, check to ensure the E-Stop hasn't been pressed. (Saftey feature!!)
             if (globalFlag == 1)
                 return;

             //get data
             for (int iy = 0; iy < ny; iy++)
             {
                 axisLetter = "X";
                 stepSize = stepx;
                 writemux(Convert.ToByte(15));
                 // writemux(Convert.ToByte(31));   //for sensor test only
                 run1point(nfre, sfre, pauseTime, filename);

                 for (int i = 1; i < numDatapoints; i++)
                 {

                     //writemux(Convert.ToByte(31-i));   //for sensor test only

                     TimeWait(pauseTime); //Calls function to delay without pausing thread execution like sleep(n) does

                     //Right before movement, again make sure E-Stop hasn't been pressed
                     if (globalFlag == 1)
                         return;

                     //Move to next point in x direction

                     try
                     {
                         this.myController.Commands.Motion.Linear(axisLetter, stepSize + returndis);
                     }
                     catch (A3200Exception exception)
                     {
                         labelErrorMessage.Text = exception.Message + "Here is a helplink: " + exception.HelpLink;
                         MessageBox.Show("Houston, we have a problem.  Your scanner dun fucked up."); //CHANGE BEFORE SUBMISSION
                         break;
                     }

                     try
                     {
                         this.myController.Commands.Motion.Linear(axisLetter, -returndis);
                     }
                     catch (A3200Exception exception)
                     {
                         labelErrorMessage.Text = exception.Message + "Here is a helplink: " + exception.HelpLink;
                         MessageBox.Show("Houston, we have a problem.  Your scanner dun fucked up."); //CHANGE BEFORE SUBMISSION
                         break;
                     }

                     run1point(nfre, sfre, pauseTime, filename);

                 }

                 //move back to initial position in x direction
                 //Move to next point
                 try
                 {
                     this.myController.Commands.Motion.Linear(axisLetter, -(numDatapoints - 1) * stepSize-returndis);
                 }
                 catch (A3200Exception exception)
                 {
                     labelErrorMessage.Text = exception.Message + "Here is a helplink: " + exception.HelpLink;
                     MessageBox.Show("Houston, we have a problem.  Your scanner dun fucked up."); //CHANGE BEFORE SUBMISSION                 
                 }

                 try
                 {
                     this.myController.Commands.Motion.Linear(axisLetter, returndis);
                 }
                 catch (A3200Exception exception)
                 {
                     labelErrorMessage.Text = exception.Message + "Here is a helplink: " + exception.HelpLink;
                     MessageBox.Show("Houston, we have a problem.  Your scanner dun fucked up."); //CHANGE BEFORE SUBMISSION                 
                 }

                 //move along y direction
                 axisLetter = "Y";
                 stepSize = stepy;
                 try
                 {
                     this.myController.Commands.Motion.Linear(axisLetter, stepSize+returndis);
                 }
                 catch (A3200Exception exception)
                 {
                     labelErrorMessage.Text = exception.Message + "Here is a helplink: " + exception.HelpLink;
                     MessageBox.Show("Houston, we have a problem.  Your scanner dun fucked up."); //CHANGE BEFORE SUBMISSION                
                 }

                 try
                 {
                     this.myController.Commands.Motion.Linear(axisLetter, -returndis);
                 }
                 catch (A3200Exception exception)
                 {
                     labelErrorMessage.Text = exception.Message + "Here is a helplink: " + exception.HelpLink;
                     MessageBox.Show("Houston, we have a problem.  Your scanner dun fucked up."); //CHANGE BEFORE SUBMISSION                
                 }
             }

             axisLetter = "Y";
             stepSize = stepy;
             try
             {
                 this.myController.Commands.Motion.Linear(axisLetter, -(ny) * stepy);
             }
             catch (A3200Exception exception)
             {
                 labelErrorMessage.Text = exception.Message + "Here is a helplink: " + exception.HelpLink;
                 MessageBox.Show("Houston, we have a problem.  Your scanner dun fucked up."); //CHANGE BEFORE SUBMISSION                
             }

         }
         private void gridscanre1mm_Click(object sender, EventArgs e)
         {
             preparetowork(true, true);
             rungridscanre1mm();
         }

         private void label54_Click(object sender, EventArgs e)
         {

         }

         private void button_lockinmultilinescan_Click(object sender, EventArgs e)
         {
             preparetowork(true, true);
             string cfilename = rtbFilefoder.Text;

             double nscan = double.Parse(richTextBox_nscans.Text);

             double dis = double.Parse(richTextBox_distancebetweenscan.Text);

            
             for (int i = 0; i < nscan; i++)
             {
                 rtbFilefoder.Text=cfilename+i.ToString();
                 

                 if (i > 0)
                 {
                     //Move to next point
                     try
                     {
                         axisLetter = "X";
                         this.myController.Commands.Motion.Linear(axisLetter, dis);
                     }
                     catch (A3200Exception exception)
                     {
                         labelErrorMessage.Text = exception.Message + "Here is a helplink: " + exception.HelpLink;
                         MessageBox.Show("Houston, we have a problem.  Your scanner dun fucked up."); //CHANGE BEFORE SUBMISSION
                         break;
                     }
                 }
                 
                 CNCOp(double.Parse(textBoxXdist.Text), double.Parse(textBoxYdist.Text), double.Parse(textBoxZdist.Text),
                    double.Parse(textBoxXstep.Text), double.Parse(textBoxYstep.Text), double.Parse(textBoxZstep.Text),
                    int.Parse(textBoxDatapoints.Text), double.Parse(textBoxPausetime.Text)); 
             }


             if (nscan > 1)
             {
                 //Move to next point
                 try
                 {
                     string axisLetter = "X";
                     this.myController.Commands.Motion.Linear(axisLetter, -(nscan-1)*dis);
                 }
                 catch (A3200Exception exception)
                 {
                     labelErrorMessage.Text = exception.Message + "Here is a helplink: " + exception.HelpLink;
                     MessageBox.Show("Houston, we have a problem.  Your scanner dun fucked up."); //CHANGE BEFORE SUBMISSION
                     
                 }
             }

               
         }

         private void button_continuesmultilinescan_Click(object sender, EventArgs e)
         {
             string filename = rtbFilefoder.Text;
             string[] sfre = rtbFrequencies.Text.Split(',');
             int nfre = sfre.Length;


             int nx = Convert.ToInt16(textBoxStepnumberX.Text);
             int ny = Convert.ToInt16(textBoxStepnumberY.Text);

             double stepx = Convert.ToDouble(textBoxStepsizeX.Text);

             double stepy = Convert.ToDouble(textBoxStepsizeY.Text);

             string direction = textBoxMoveDirection.Text.ToUpper();
             double distance = Convert.ToDouble(textBoxMoveDistance.Text);
             string[] sspeed = Textboxspeedlist.Text.Split(',');
             int nspeed = sspeed.Length;
             double speed = 20;
             int samplefre = Convert.ToInt32(textBoxSamplefrequency.Text);
             int noflines = 32;
             double distancebetweenlines = Convert.ToDouble(textBoxDistancebetweenlines.Text);


             preparetowork(true, true);

             string cfilename = rtbFilefoder.Text;

             double nscan = double.Parse(richTextBox_nscans.Text);

             double dis = double.Parse(richTextBox_distancebetweenscan.Text);


             for (int i = 0; i < nscan; i++)
             {
                 rtbFilefoder.Text = cfilename + i.ToString();


                 if (i > 0)
                 {
                     //Move to next point
                     try
                     {
                         axisLetter = "X";
                         this.myController.Commands.Motion.Linear(axisLetter, dis);
                     }
                     catch (A3200Exception exception)
                     {
                         labelErrorMessage.Text = exception.Message + "Here is a helplink: " + exception.HelpLink;
                         MessageBox.Show("Houston, we have a problem.  Your scanner dun fucked up."); //CHANGE BEFORE SUBMISSION
                         break;
                     }
                 }


                         for (int ispeed = 0; ispeed < nspeed; ispeed++)
                         {
                             speed = Convert.ToDouble(sspeed[ispeed]);
                          
                             for (int ifre = 0; ifre < nfre; ifre++)
                             {
                                 double f = Convert.ToDouble(sfre[ifre]);
                                 setfrquency(f, f);

                                 for (byte iline = 0; iline < noflines; iline++)
                                 {
                                     filename = rtbFilefoder.Text;
                                     writemux(iline);
                                     continuemove1line(direction, distance, speed, samplefre, filename + sfre[ifre]);

                                 }

                             }

                         }

           
             }


             if (nscan > 1)
             {
                 //Move to next point
                 try
                 {
                     string axisLetter = "X";
                     this.myController.Commands.Motion.Linear(axisLetter, -(nscan - 1) * dis);
                 }
                 catch (A3200Exception exception)
                 {
                     labelErrorMessage.Text = exception.Message + "Here is a helplink: " + exception.HelpLink;
                     MessageBox.Show("Houston, we have a problem.  Your scanner dun fucked up."); //CHANGE BEFORE SUBMISSION

                 }
             }

         }

         private void run1linewithoutlockin(int nfre, string[] fre, int nsensor, double pauseTime, string filename)
         {
             DateTime Tthen = DateTime.Now; //Tthen = current time

             
             int i, j;
             
             double f;
             for (i = 0; i < nfre; i++)
             {
                 f = Convert.ToDouble(fre[i]);
                 string fn = filename + Convert.ToString(fre[i]) + "Hz.txt";
                 setfrquency(f, f);
                 for (j = 0; j < nsensor; j++)
                 {
                     if (nsensor > 1)
                     {
                         writemux(Convert.ToByte(j));
                     }

                     // TimeWait(pauseTime);

                     string channel1 = "dev1/ai3";
                     string channel2 = "dev1/ai2";
                     double lowerlim = -5;
                     double upperlim = 5;
                                          
                     int samplefre = Convert.ToInt32(textBoxSamplefrequency.Text);
                     int samples = Convert.ToInt32(1/f*samplefre*20);   // 20 periods of data
                     
                     double[,] data = DAQ_RandI(samples, samplefre, channel1, channel2, lowerlim, upperlim);
                     // channel 1 for reference, channel 2 for data
                     double[] ref0 = new double[samples];
                     double[] ref1 = new double[samples];
                     //double[] sig = new double[samples];
                     double[] arraycos = new double[samples];
                     double[] arraysin = new double[samples];

                     int nqup=Convert.ToInt32((1/f)*samplefre/4);

                     for (int sam = 0; sam < samples; sam++)
                     {
                         if (data[0, sam] > 1.5) ref0[sam] = 1;
                         else ref0[sam] = -1;
                     }

                     for (int sam = 0; sam < samples-nqup; sam++)
                         ref1[sam] = ref0[sam+nqup];
                     for (int sam = 0; sam < nqup; sam++)
                         ref1[sam + samples - nqup] = ref0[sam];

                     for (int sam = 0; sam < samples; sam++)
                     {
                         if (ref0[sam] < 0) 
                             arraycos[sam] = -data[1,sam];
                         else 
                             arraycos[sam] = data[1,sam];

                         if (ref1[sam] < 0)
                             arraysin[sam] = -data[1,sam];
                         else
                             arraysin[sam] = data[1,sam];
                     }

                     double re = 0;
                     double im = 0;

                     for (int sam = 0; sam < samples; sam++)
                     {
                         re = re + arraycos[sam];
                         im = im + arraysin[sam];
                     }
                     re = re / samples;
                     im = im / samples;

                     string datapair = Convert.ToString(re) + "," + Convert.ToString(im);

                     

                         using (StreamWriter writer = new StreamWriter(fn, true))
                         {

                                 writer.WriteLine(datapair);
                         }

                     /*
                     using (System.IO.StreamWriter file = new System.IO.StreamWriter(fn)) //writes data to filepath
                     {
                         foreach (double line in data)
                         {
                             file.WriteLine(line);
                         }
                     }
                      */
                 }
             }
         }

         private void arraysensorlinescannolockinstep(double Xdist, double Ydist, double Zdist, double Xstep, double Ystep, double Zstep, int numDatapoints, double pauseTime)
         {

             double stepSize = 0; //Size of step to be iterated through
             //int flag = 0; //Tracks user input, where 1 = input distance, 2 = input step size 

             string filename = rtbFilefoder.Text;
             string[] sfre = rtbFrequencies.Text.Split(',');
             int nfre = sfre.Length;
             int nsensor = Convert.ToInt16(rtbNumberofsensors.Text);


             if ((Xstep != 0) || (Ystep != 0) || (Zstep != 0)) //if the user is inputting a step size and number of datapoints
             {
                 if (Xstep != 0)
                 {
                     axisLetter = "X";
                     stepSize = Xstep;
                 }
                 else if (Ystep != 0)
                 {
                     axisLetter = "Y";
                     stepSize = Ystep;
                 }
                 else if (Zstep != 0)
                 {
                     axisLetter = "Z";
                     stepSize = Zstep;
                 }
                 else
                 {
                     MessageBox.Show("Congrats, you somehow managed to completely screw this program, narrowly missing else if statements and catches.  You should feel proud of yourself.  Unfortunately execution has been halted.");
                     return;
                 }


             }
             //After movement has been determined, check to ensure the E-Stop hasn't been pressed. (Saftey feature!!)
             if (globalFlag == 1)
                 return;



             //get data

             run1linewithoutlockin(nfre, sfre, nsensor, pauseTime, filename);
             for (int i = 1; i < numDatapoints; i++)
             {



                 TimeWait(pauseTime); //Calls function to delay without pausing thread execution like sleep(n) does

                 //Right before movement, again make sure E-Stop hasn't been pressed
                 if (globalFlag == 1)
                     return;

                 //Move to next point
                 try
                 {
                     this.myController.Commands.Motion.Linear(axisLetter, stepSize);
                 }
                 catch (A3200Exception exception)
                 {
                     labelErrorMessage.Text = exception.Message + "Here is a helplink: " + exception.HelpLink;
                     MessageBox.Show("Houston, we have a problem.  Your scanner dun fucked up."); //CHANGE BEFORE SUBMISSION
                     break;
                 }
                 run1linewithoutlockin(nfre, sfre, nsensor, pauseTime, filename);

             }

             //move back to initial position
             //Move to next point
             try
             {
                 this.myController.Commands.Motion.Linear(axisLetter, -(numDatapoints - 1) * stepSize);
             }
             catch (A3200Exception exception)
             {
                 labelErrorMessage.Text = exception.Message + "Here is a helplink: " + exception.HelpLink;
                 MessageBox.Show("Houston, we have a problem.  Your scanner dun fucked up."); //CHANGE BEFORE SUBMISSION                 
             }

         }
         private void button_multilinenolockin_Click(object sender, EventArgs e)
         {
             preparetowork(true, true);
             string cfilename = rtbFilefoder.Text;

             double nscan = double.Parse(richTextBox_nscans.Text);

             double dis = double.Parse(richTextBox_distancebetweenscan.Text);


             for (int i = 0; i < nscan; i++)
             {
                 rtbFilefoder.Text = cfilename + i.ToString();


                 if (i > 0)
                 {
                     //Move to next point
                     try
                     {
                         axisLetter = "X";
                         this.myController.Commands.Motion.Linear(axisLetter, dis);
                     }
                     catch (A3200Exception exception)
                     {
                         labelErrorMessage.Text = exception.Message + "Here is a helplink: " + exception.HelpLink;
                         MessageBox.Show("Houston, we have a problem.  Your scanner dun fucked up."); //CHANGE BEFORE SUBMISSION
                         break;
                     }
                 }
                 arraysensorlinescannolockinstep(double.Parse(textBoxXdist.Text), double.Parse(textBoxYdist.Text), double.Parse(textBoxZdist.Text),
                 double.Parse(textBoxXstep.Text), double.Parse(textBoxYstep.Text), double.Parse(textBoxZstep.Text),
                 int.Parse(textBoxDatapoints.Text), double.Parse(textBoxPausetime.Text));
                 
             }


             if (nscan > 1)
             {
                 //Move to next point
                 try
                 {
                     string axisLetter = "X";
                     this.myController.Commands.Motion.Linear(axisLetter, -(nscan - 1) * dis);
                 }
                 catch (A3200Exception exception)
                 {
                     labelErrorMessage.Text = exception.Message + "Here is a helplink: " + exception.HelpLink;
                     MessageBox.Show("Houston, we have a problem.  Your scanner dun fucked up."); //CHANGE BEFORE SUBMISSION

                 }
             }
         }

    }

}