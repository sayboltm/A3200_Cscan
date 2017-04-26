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

	}
}
