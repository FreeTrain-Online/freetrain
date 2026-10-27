// 2008.08.30 YZ New create
// 2008.09.06 YZ Added stationNotifyHandlers remove in Dispose
// 2008.11.11 YZ Modified station notify handler name
// 2008.11.22 YZ Modified station add/remove notify handler to event handler
using System;
using System.Diagnostics;
using System.Drawing;
using System.Collections;
using System.ComponentModel;
using System.Windows.Forms;
using freetrain.world;
using freetrain.world.rail;
using freetrain.framework;
using freetrain.views.map;

namespace freetrain.views
{
	/// <summary>
	/// Displays Station List
	/// </summary>
	public class StationList : Form {
        private ArrayList stationHashList = new ArrayList();                    // Station name and hashcode list
        private ListBox StationListBox;
		private System.ComponentModel.Container components = null;

        /// <summary>StationList constructor</summary>
		public StationList() {
			InitializeComponent();
            viewStationList();                                                  // View station list

#region YZ_20081122_MODIFIED
#region YZ_20081111_MODIFIED
//          World.world.stations.stationNotifyHandlers.add((StationNotifyHandler)onStationNotify);            
//          World.world.stations.stationNotifyHandlers2.add((StationNotifyHandler2)onStationNotify);            
#endregion
            World.onNewWorld += new EventHandler(onNewWorld);                   // Add create new world event handler
            registHandlers();                                                   // Regist all handlers
#endregion
        }

#region YZ_20081122_ADDED
        /// <summary>Create new world event handler</summary>
        /// <value>object    sender</value>
        /// <value>EventArgs ea</value>
        private void onNewWorld(object sender, EventArgs ea) {
            viewStationList();                                                  // View station list
            registHandlers();                                                   // Regist all handlers
        }

        /// <summary>Regist all event handlers</summary>
        private void registHandlers() {
                                                                                // Add station construction event handler
            World.StationCollection.OnStationConstruction += new EventHandler(onStationConstruction);

            foreach(Station stationItem in World.world.stations) {              // Loop in stations
                                                                                // Add station change event handler
                stationItem.stationChangeListeners.add(onStationChanged);
            }
        }

        /// <summary>Unregist all event handlers</summary>
        private void unregistHandlers() {
            foreach(Station stationItem in World.world.stations) {              // Loop in stations
                                                                                // Delete station change event handler
                stationItem.stationChangeListeners.remove(onStationChanged);
            }
                                                                                // Delete station construction event handler
            World.StationCollection.OnStationConstruction -= new EventHandler(onStationConstruction);
            World.onNewWorld -= new EventHandler(onNewWorld);                   // Delete create new world event handler
        }
#endregion

        /// <summary>Station change notify event handler</summary>
#region YZ_20081122_MODIFIED
//      public void onStationNotify() {
        private void onStationConstruction(object sender, EventArgs ea) {
            viewStationList();                                                  // View station list

            if (sender != null) {                                               // If sender not equal null(station creat)
                                                                                // Add station change handler
                ((Station)sender).stationChangeListeners.add(onStationChanged);
            }
        }

        private void onStationChanged() {
            viewStationList();                                                  // View station list
        }
#endregion

        // View station to station listbox
        private void viewStationList() {
#region YZ_20081122_ADDED
            this.StationListBox.BeginUpdate();                                  // Begin listbox update
#endregion

            this.StationListBox.Items.Clear();                                  // Clear station list box items 
            this.stationHashList.Clear();                                       // Clear station hashcode list

            foreach(Station stationItem in World.world.stations) {
                this.StationListBox.Items.Add(stationItem.name);                // Add station name to listbox
                                                                                // Add station name and hashcode to ArrayList
                this.stationHashList.Add(stationItem.name + " " + stationItem.GetHashCode().ToString());
            }
            this.stationHashList.Sort();                                        // Sort station hashcode list

#region YZ_20081122_ADDED
            this.StationListBox.EndUpdate();                                    // End listbox update
#endregion
        }

		/// <summary>StationList disposed</summary>
		protected override void Dispose( bool disposing )
		{
#region YZ_20081122_MODIFIED
#region YZ_20081111_MODIFIED
//          World.world.stations.stationNotifyHandlers.remove((StationNotifyHandler)onStationNotify);            
//          World.world.stations.stationNotifyHandlers2.remove((StationNotifyHandler2)onStationNotify);            
            unregistHandlers();                                                 // Unregist all handlers
#endregion
#endregion

            if (disposing && components != null) {
			    components.Dispose();
            }
			base.Dispose(disposing);
		}

		#region Windows Form Designer generated code
		/// <summary>
		/// デザイナ サポートに必要なメソッドです。このメソッドの内容を
		/// コード エディタで変更しないでください。
		/// </summary>
		private void InitializeComponent()
		{
        this.StationListBox = new System.Windows.Forms.ListBox();
        this.SuspendLayout();
        // 
        // StationListBox
        // 
        this.StationListBox.Dock = System.Windows.Forms.DockStyle.Fill;
        this.StationListBox.FormattingEnabled = true;
        this.StationListBox.ItemHeight = 14;
        this.StationListBox.Location = new System.Drawing.Point(0,0);
        this.StationListBox.Name = "StationListBox";
        this.StationListBox.Size = new System.Drawing.Size(192,270);
        this.StationListBox.Sorted = true;
        this.StationListBox.TabIndex = 0;
        this.StationListBox.DoubleClick += new System.EventHandler(this.StationListBox_DoubleClick);
        // 
        // StationList
        // 
        this.AutoScaleBaseSize = new System.Drawing.Size(6,15);
        this.ClientSize = new System.Drawing.Size(192,271);
        this.Controls.Add(this.StationListBox);
        this.MaximizeBox = false;
        this.MinimizeBox = false;
        this.Name = "StationList";
        this.ShowInTaskbar = false;
        this.Text = "駅一覧";
        this.ResumeLayout(false);

		}
		#endregion

        // Double-click station listbox event handler
        private void StationListBox_DoubleClick(object sender,EventArgs e) {
            MapView _mapView;

            foreach(Station st in World.world.stations) {
                if ((st.name + " " + st.GetHashCode().ToString()) == (string)this.stationHashList[this.StationListBox.SelectedIndex]) {
                    foreach(IView view in MainWindow.mainWindow.getAllViews()) {
        				if (view is MapView) {
                            _mapView = (MapView)view;
                            if (_mapView.IsActive == true) {
                                _mapView.moveTo(st.baseLocation);
                            }
            			}
                    }    
                }
            }
        }
	}
}
