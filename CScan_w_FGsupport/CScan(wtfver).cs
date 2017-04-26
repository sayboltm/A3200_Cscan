using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Windows.Forms;

using Aerotech.A3200;
using Aerotech.A3200.Exceptions;
using Aerotech.A3200.Status;
using Aerotech.A3200.Variables;
using Aerotech.A3200.Tasks;
using Aerotech.A3200.Information;
using Aerotech.Common;
using Aerotech.Common.Collections;


namespace GUI
{
	public partial class formGUI : Form
	{
		#region Fields

		private Controller myController;
		private int axisIndex;
		private int taskIndex;
        int axisNum; //1,2,3 = x,y,z (used for controlling which axis to move in CNCOp)
        string axisLetter;
        

		#endregion Fields

		#region Constructors

		public formGUI()
		{
			InitializeComponent();
		}

		private void formGUI_Load(object sender, EventArgs e)
		{
			checkControllerConnected.Checked = false;
			EnableControls(false);
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
		private void EnableControls(bool enable)
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
			labelAxisState.Text = e.Data[this.axisIndex].DriveStatus.Enabled.ToString();
			labelAxisHomed.Text = e.Data[this.axisIndex].AxisStatus.Homed.ToString();
			labelAxisFault.Text = (!e.Data[this.axisIndex].AxisFault.None).ToString();
			labelAxisPosition.Text = e.Data[this.axisIndex].PositionFeedback.ToString();
			labelAxisSpeed.Text = e.Data[this.axisIndex].VelocityFeedback.ToString();
		}

		#endregion Methods

		#region WindowsEvents

		private void buttonConnectController_Click(object sender, EventArgs e)
		{
			try
			{
				// Connect to A3200 controller.  
				this.myController = Controller.Connect();
				checkControllerConnected.Checked = true;
				EnableControls(true);

				// populate axis names
				foreach (AxisInfo axis in this.myController.Information.Axes)
				{
					comboAxis.Items.Add(axis.Name);
				}
				this.axisIndex = 0;
				comboAxis.SelectedIndex = this.axisIndex;

				// populate task names
				foreach (Task task in this.myController.Tasks)
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


        //ask Matt how to delete these properly
        #region oldshit
        private void checkControllerConnected_CheckedChanged(object sender, EventArgs e)
        {

        }

        private void ButtonFreerunCW_Click(object sender, EventArgs e)
        {

        }

        private void ButtonFreerunCCW_Click(object sender, EventArgs e)
        {

        }

        #endregion oldshit 


        //X Y and Z stuff concerns jogging of axes
        #region X stuff
        private void button1_MouseDown(object sender, EventArgs e) //dont know why didnt rename to "buttonL"
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
                 this.myController.Commands[this.taskIndex].Motion.FreeRun("X", double.Parse(textFreerunSpeed.Text));
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
        #endregion X stuff

        #region Y stuff
         
         private void buttonFW_MouseDown(object sender, MouseEventArgs e)
         {
             try
             {
                 this.myController.Commands[this.taskIndex].Motion.FreeRun("Y", -double.Parse(textFreerunSpeed.Text));
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
                 this.myController.Commands[this.taskIndex].Motion.FreeRun("Y", double.Parse(textFreerunSpeed.Text));
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
         #endregion Y stuff

        #region Z stuff
         private void buttonDN_MouseDown(object sender, MouseEventArgs e)
         {
             try
             {
                 this.myController.Commands[this.taskIndex].Motion.FreeRun("Z", -double.Parse(textFreerunSpeed.Text));
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
                 this.myController.Commands[this.taskIndex].Motion.FreeRun("Z", double.Parse(textFreerunSpeed.Text));
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
        #endregion Z stuff


        //Contains the main movement function
         #region MoarFunc
         private void CNCOp(double Xdist, double Ydist, double Zdist, double Xstep, double Ystep, double Zstep, int numDatapoints, double pauseTime) //Main movement function.  Executes when buttonExe is clicked.
         {
             double stepSize = 0;
             //statements to make sure code doesn't screw itself
             #region robustness

             /*
             int NumBoxesEnabled = 0;  
             if ((double.Parse(textBoxXdist.Text) != 0) || (double.Parse(textBoxXstep.Text) != 0))
             {
                 labelDiagnostic.Text = "X dist is not zero.";
                 AxisNum = 1;
                 NumBoxesEnabled++;
                 textBoxYdist.Enabled = false;
                 textBoxZdist.Enabled = false;
             }

             else if (double.Parse(textBoxYdist.Text) !=0)
             {
                 labelDiagnostic.Text = "Y dist is not zero.";
                 AxisNum = 2;
                 NumBoxesEnabled++;
                 textBoxXdist.Enabled = false;
                 textBoxZdist.Enabled = false;
             }

             else if (double.Parse(textBoxZdist.Text) != 0)
             {
                 labelDiagnostic.Text = "Z dist is not zero.";
                 AxisNum = 3;
                 NumBoxesEnabled++;
                 textBoxXdist.Enabled = false;
                 textBoxYdist.Enabled = false;
                 
             }
             else
             {
                 labelDiagnostic.Text = "isnull";
                 textBoxDatapoints.Text = "All boxes zero supposedly";
                 AxisNum = 0;
                 textBoxXdist.Enabled = true;
                 textBoxYdist.Enabled = true;
                 textBoxZdist.Enabled = true;
                 NumBoxesEnabled--;
             }
              */

             /*
             if (double.Parse(textBoxYdist.Text) != 0)
             {
                 
             }
             else
             {
                 
             }


             if (double.Parse(textBoxZdist.Text) != 0)
             {
                 
             }
             else
             {
                 labelDiagnostic.Text = "isnull";
                 // c AxisNum = 0;
                 NumBoxesEnabled--;
             }
             textBoxDatapoints.Text = NumBoxesEnabled.ToString();
              */

             #endregion robustness
 
             //determine which axis is to be moved (currently only one at a time)
             switch (axisNum)
             {
                 case 0: //No axes selected
                     MessageBox.Show(new Form() { TopMost = true }, "Cannot move machine without directions. PROGRAM ME DAMNIT!"); //CHANGE BEFORE SUBMISSION
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



             //break up total distance into datapoints
             /*<pseudocode>
              * for distance input:
              * xdist = 10 mm
              * num datapoints = 5
              * xdist/datapoints = every 2 mm pause //pausedist
              * 
              * for i = 1:numdatapoints
              * Linear(axisLetter, stepSize)
              * 
              * - or -
              * for step input:
              * xstepsize = 2 mm
              * num datapoints = 5
              * 
              * for i = 1:numdatapoints
              * linear(axisLetter, xstepsize)
              * 
              */
             if ( (Xdist != 0) || (Ydist != 0) || (Zdist != 0)) //if true then user is inputting a distance
             {
                 stepSize = Xdist / numDatapoints;
             }
             
             
             //move and pause
             for (int i = 1; i < numDatapoints; i++)
             {
                 try
                 {
                     this.myController.Commands.Motion.Linear(axisLetter, stepSize);
                 }
                 catch (A3200Exception exception)
                 {
                     labelErrorMessage.Text = exception.HelpLink;
                     MessageBox.Show("Houston, we have a problem.  Your scanner dun fucked up."); //CHANGE BEFORE SUBMISSION
                     break;
                 }

                 DateTime Tthen = DateTime.Now;
                 do
                 {
                     Application.DoEvents();
                 } while (Tthen.AddSeconds(pauseTime) > DateTime.Now);
             }
             

             /*
              * Useful code?:
             DateTime Tthen = DateTime.Now;
             do
             {
                 Application.DoEvents();
             } while (Tthen.AddSeconds(5) > DateTime.Now);
             */

             /*
                      * <pseudocode>
                      * //make sure only one axis used at a time (for now)
                      * case statement etc
                      * 
                      * //Here only X is used
                      * Xincrement = X/textBoxDatapoints
                      * for i=1:datapoints
                      * this.myController.Commands.Motion.Linear("Xincrement",X); SYNTAX: Linear(AxisMask axisMask, double Distance)
                      * i++
                      * </pseudocode>
                      */

             //Console.WriteLine(textFreerunSpeed.Text);
            // if (NumBoxesEnabled <= 1)
             //{
             /*
             try
                 {
                     this.myController.Commands.Motion.Linear("X", X);
                     
                     

                 }
                 catch (A3200Exception exception)
                 {
                     //labelErrorMessage.Text = exception.Message;
                     labelErrorMessage.Text = exception.HelpLink;

                 }
             //}
             /*
             else
             {
                 MessageBox.Show(new Form() { TopMost = true }, "CScan V1.0 BETA can only move one axis at a time.  Please set the others to '0' and try again");
             }
              */
    

         }
         #endregion MoarFunc

         private void buttonExe_Click(object sender, EventArgs e)
         {
              
             CNCOp(double.Parse(textBoxXdist.Text), double.Parse(textBoxYdist.Text), double.Parse(textBoxZdist.Text), 
                 double.Parse(textBoxXstep.Text), double.Parse(textBoxYstep.Text), double.Parse(textBoxZstep.Text), 
                 int.Parse(textBoxDatapoints.Text), double.Parse(textBoxPausetime.Text)); 
         }

         private void buttonStop_Click(object sender, EventArgs e)
         {
             try
             {
                 //change to abort "current axis"
                 this.myController.Commands[this.taskIndex].Motion.Abort("X");
                 this.myController.Commands[this.taskIndex].Motion.Abort("Y");
                 this.myController.Commands[this.taskIndex].Motion.Abort("Z");
             }
             catch(A3200Exception exception)
             {
                 labelErrorMessage.Text = exception.Message;
             }
         }




        // robustness
         private void textBoxXdist_TextChanged(object sender, EventArgs e)
         {
             double comp; //Necessary because must have a double for TryParse to out with
             bool result = double.TryParse(textBoxXdist.Text, out comp);
             if (comp == 0) //if X distance box is not used, enable all others
             {
                 textBoxYdist.Enabled = true;
                 textBoxZdist.Enabled = true;
                 groupBoxStepsize.Enabled = true;
                 axisNum = 0; //No axes used in this state
                 
             }
             else  //X distance used, disable others (for now)
             {
                 textBoxYdist.Enabled = false;
                 textBoxZdist.Enabled = false;
                 groupBoxStepsize.Enabled = false; //Disable the step size input if using distance
                 axisNum = 1; //Corresponds to a movement on the X axis
             }
             
         }

         private void textBoxYdist_TextChanged(object sender, EventArgs e)
         {
             double comp;
             bool result = double.TryParse(textBoxYdist.Text, out comp);
             if (comp == 0) 
             {
                 textBoxXdist.Enabled = true;
                 textBoxZdist.Enabled = true;
                 groupBoxStepsize.Enabled = true;
                 axisNum = 0;
             }
             else
             {
                 textBoxXdist.Enabled = false;
                 textBoxZdist.Enabled = false;
                 groupBoxStepsize.Enabled = false;
                 axisNum = 2; //Corresponds to a movement on the Y axis
             }

         }

         private void textBoxZdist_TextChanged(object sender, EventArgs e)
         {
             double comp;
             bool result = double.TryParse(textBoxZdist.Text, out comp);
             if (comp == 0)
             {
                 textBoxXdist.Enabled = true;
                 textBoxYdist.Enabled = true;
                 groupBoxStepsize.Enabled = true;
                 axisNum = 0;
             }
             else
             {
                 textBoxXdist.Enabled = false;
                 textBoxYdist.Enabled = false;
                 groupBoxStepsize.Enabled = false;
                 axisNum = 3; //Corresponds to a movement on the Z axis
             }
         }

         private void textBoxXstep_TextChanged(object sender, EventArgs e)
         {
             double comp;
             bool result = double.TryParse(textBoxXstep.Text, out comp);
             if (comp == 0) //if X stepsize not used, enable all
             {
                 textBoxYstep.Enabled = true;
                 textBoxZstep.Enabled = true;
                 groupBoxDistance.Enabled = true;
                 axisNum = 0;
             }
             else //if X stepsize used, disable all others
             {
                 textBoxYstep.Enabled = false;
                 textBoxZstep.Enabled = false;
                 groupBoxDistance.Enabled = false; //disable distance input if using steps
                 axisNum = 1; //Corresponds to a movement on the X axis (again)
             }

         }

         private void textBoxYstep_TextChanged(object sender, EventArgs e)
         {
             double comp;
             bool result = double.TryParse(textBoxYstep.Text, out comp);
             if (comp == 0)
             {
                 textBoxXstep.Enabled = true;
                 textBoxZstep.Enabled = true;
                 groupBoxDistance.Enabled = true;
                 axisNum = 0;
             }
             else
             {
                 textBoxXstep.Enabled = false;
                 textBoxZstep.Enabled = false;
                 groupBoxDistance.Enabled = false;
                 axisNum = 2; // Y
             }
         }

         private void textBoxZstep_TextChanged(object sender, EventArgs e)
         {
             double comp;
             bool result = double.TryParse(textBoxZstep.Text, out comp);
             if (comp == 0)
             {
                 textBoxXstep.Enabled = true;
                 textBoxYstep.Enabled = true;
                 groupBoxDistance.Enabled = true;
                 axisNum = 0;
             }
             else
             {
                 textBoxXstep.Enabled = false;
                 textBoxYstep.Enabled = false;
                 groupBoxDistance.Enabled = false;
                 axisNum = 3; // Z
             }
         }






 


    }

}



