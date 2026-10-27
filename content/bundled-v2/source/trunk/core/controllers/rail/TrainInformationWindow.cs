// 2008.11.21 YZ Modify minimum window size
// 2010.03.29 riorio rename TrainTrackingWindow.cs -> TrainInformationWindow.cs
using System;
using System.Diagnostics;
using System.Drawing;
using System.Collections;
using System.ComponentModel;
using System.Windows.Forms;
using freetrain.framework;
using freetrain.util.command;
using freetrain.world;
using freetrain.world.rail;

namespace freetrain.controllers.rail
{
	/// <summary>
	/// Window that informatoin a train.
	/// </summary>
	public class TrainInformationWindow : Form
	{
		public TrainInformationWindow() {
			this.train = null;
			InitializeComponent();

			new Command( commands )
				.addUpdateHandler( new CommandHandler(updateTrackButton) )
				.addExecuteHandler( new CommandHandlerNoArg(onMove) )
				.commandInstances.AddAll( buttonTrack );
		}

		private readonly CommandManager commands = new CommandManager();

		private Train train;

		#region Windows Form Designer generated code
		private System.Windows.Forms.Label stateBox;
		private System.Windows.Forms.Label passengerBox;
		private System.Windows.Forms.Label nameBox;
		private System.Windows.Forms.Button buttonSelect;
		private System.Windows.Forms.Label label1;
		private System.Windows.Forms.Label label2;
		private System.Windows.Forms.Label label5;
        private Label label3;
        private Label capacityBox;
        private Label lengthBox;
        private Label label7;
		private System.Windows.Forms.Button buttonTrack;

		/// <summary>
		/// 使用されているリソースに後処理を実行します。
		/// </summary>
		protected override void Dispose( bool disposing ) {
			if( disposing && components != null)
				components.Dispose();
			base.Dispose( disposing );
		}

		/// <summary>
		/// 必要なデザイナ変数です。
		/// </summary>
		private System.ComponentModel.Container components = null;

		/// <summary>
		/// デザイナ サポートに必要なメソッドです。このメソッドの内容を
		/// コード エディタで変更しないでください。
		/// </summary>
		private void InitializeComponent()
		{
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.stateBox = new System.Windows.Forms.Label();
            this.passengerBox = new System.Windows.Forms.Label();
            this.label5 = new System.Windows.Forms.Label();
            this.buttonTrack = new System.Windows.Forms.Button();
            this.nameBox = new System.Windows.Forms.Label();
            this.buttonSelect = new System.Windows.Forms.Button();
            this.label3 = new System.Windows.Forms.Label();
            this.capacityBox = new System.Windows.Forms.Label();
            this.lengthBox = new System.Windows.Forms.Label();
            this.label7 = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.Location = new System.Drawing.Point(3, 15);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(48, 16);
            this.label1.TabIndex = 0;
            this.label1.Text = "列車名：";
            this.label1.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // label2
            // 
            this.label2.Location = new System.Drawing.Point(1, 40);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(48, 16);
            this.label2.TabIndex = 2;
            this.label2.Text = "状態：";
            this.label2.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // stateBox
            // 
            this.stateBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.stateBox.Location = new System.Drawing.Point(50, 39);
            this.stateBox.Name = "stateBox";
            this.stateBox.Size = new System.Drawing.Size(99, 19);
            this.stateBox.TabIndex = 3;
            this.stateBox.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // passengerBox
            // 
            this.passengerBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.passengerBox.Location = new System.Drawing.Point(57, 97);
            this.passengerBox.Name = "passengerBox";
            this.passengerBox.Size = new System.Drawing.Size(99, 16);
            this.passengerBox.TabIndex = 5;
            this.passengerBox.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // label5
            // 
            this.label5.Location = new System.Drawing.Point(1, 97);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(50, 16);
            this.label5.TabIndex = 4;
            this.label5.Text = "乗客数：";
            this.label5.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // buttonTrack
            // 
            this.buttonTrack.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonTrack.Enabled = false;
            this.buttonTrack.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.buttonTrack.Location = new System.Drawing.Point(107, 127);
            this.buttonTrack.Name = "buttonTrack";
            this.buttonTrack.Size = new System.Drawing.Size(64, 24);
            this.buttonTrack.TabIndex = 6;
            this.buttonTrack.Text = "移動(&M)";
            // 
            // nameBox
            // 
            this.nameBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.nameBox.Location = new System.Drawing.Point(51, 8);
            this.nameBox.Name = "nameBox";
            this.nameBox.Size = new System.Drawing.Size(91, 30);
            this.nameBox.TabIndex = 1;
            this.nameBox.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // buttonSelect
            // 
            this.buttonSelect.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonSelect.Font = new System.Drawing.Font("Webdings", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(2)));
            this.buttonSelect.Location = new System.Drawing.Point(155, 8);
            this.buttonSelect.Name = "buttonSelect";
            this.buttonSelect.Size = new System.Drawing.Size(16, 16);
            this.buttonSelect.TabIndex = 7;
            this.buttonSelect.Text = "6";
            this.buttonSelect.TextAlign = System.Drawing.ContentAlignment.BottomCenter;
            this.buttonSelect.Click += new System.EventHandler(this.buttonSelect_Click);
            // 
            // label3
            // 
            this.label3.Location = new System.Drawing.Point(1, 67);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(48, 16);
            this.label3.TabIndex = 8;
            this.label3.Text = "定員：";
            this.label3.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // capacityBox
            // 
            this.capacityBox.Location = new System.Drawing.Point(49, 58);
            this.capacityBox.Name = "capacityBox";
            this.capacityBox.Size = new System.Drawing.Size(44, 33);
            this.capacityBox.TabIndex = 9;
            this.capacityBox.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // lengthBox
            // 
            this.lengthBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.lengthBox.Location = new System.Drawing.Point(125, 66);
            this.lengthBox.Name = "lengthBox";
            this.lengthBox.Size = new System.Drawing.Size(46, 18);
            this.lengthBox.TabIndex = 11;
            this.lengthBox.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // label7
            // 
            this.label7.Location = new System.Drawing.Point(87, 66);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(40, 17);
            this.label7.TabIndex = 10;
            this.label7.Text = "両数：";
            this.label7.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // TrainInformationWindow
            // 
            this.AutoScaleBaseSize = new System.Drawing.Size(5, 12);
            this.ClientSize = new System.Drawing.Size(173, 156);
            this.Controls.Add(this.lengthBox);
            this.Controls.Add(this.label7);
            this.Controls.Add(this.capacityBox);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.buttonSelect);
            this.Controls.Add(this.buttonTrack);
            this.Controls.Add(this.passengerBox);
            this.Controls.Add(this.label5);
            this.Controls.Add(this.stateBox);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.nameBox);
            this.Controls.Add(this.label1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.SizableToolWindow;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "TrainInformationWindow";
            this.Text = "列車の情報";
            this.ResumeLayout(false);

		}
		#endregion


		protected override void OnClosed(EventArgs e) {
			if(train!=null)
				train.nonPersistentStateListeners -= new TrainHandler(onStateChanged);
		}

		private void onStateChanged( Train tr ) {
			Debug.Assert( tr==train );
			stateBox.Text = train.stateDisplayText;
			nameBox.Text = train.name;
			
            lengthBox.Text = train.length.ToString();
            capacityBox.Text = train.passengerCapacity.ToString();

            string ratio = "-";
			if( train.passengerCapacity!=0 )
				ratio = (train.passenger*100/train.passengerCapacity).ToString();
			passengerBox.Text = string.Format("{0} ({1}%)", train.passenger, ratio );
			commands.updateAll();
		}

		private void buttonSelect_Click(object sender, EventArgs e) {
			ContextMenu m = new ContextMenu();
			populateMenu( m.MenuItems, World.world.rootTrainGroup );
			m.Show( buttonSelect, new Point(0,buttonSelect.Height) );
		}

		private void populateMenu( Menu.MenuItemCollection menu, TrainGroup group ) {
			foreach( TrainItem item in group.items ) {
				MenuItem mi = new MenuItem( item.name );
				menu.Add(mi);

				if( item is TrainGroup ) {
					populateMenu( mi.MenuItems, (TrainGroup)item );
				} else {
					mi.Click += new EventHandler(new MenuHandler(this,(Train)item).handler);
				}
			}
		}

		private class MenuHandler {
			internal MenuHandler( TrainInformationWindow o, Train tr ) { this.owner=o; this.train=tr; }
			private readonly Train train;
			private readonly TrainInformationWindow owner;
			internal void handler( object sender, EventArgs e ) {
				owner.selectTrain(train);
			}
		}

		private void selectTrain( Train newTrain ) {
			if(train!=null)
				train.nonPersistentStateListeners -= new TrainHandler(onStateChanged);
			train = newTrain;
			// update the window now.
			onStateChanged(train);
			// make sure that we will update the window in a timely fashion
			train.nonPersistentStateListeners += new TrainHandler(onStateChanged);
		}

		private void updateTrackButton( Command cmd ) {
			cmd.Enabled = train!=null && train.isPlaced;
		}
		private void onMove() {
			if( train.head.state.isUnplaced )		return;
			if( MainWindow.primaryMapView==null )	return;
			
			MainWindow.primaryMapView.moveTo( train.head.state.asPlaced().location );
		}

	}
}
