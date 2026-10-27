// 2008.11.21 YZ Modify dialog size
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using System.Runtime.Serialization.Formatters.Binary;
using freetrain.contributions.rail;
using freetrain.framework;
using freetrain.world;
using freetrain.world.rail;

namespace freetrain.controllers.rail
{
	/// <summary>
	/// Shows a dialog that allows the user to
	/// maintain train controller dialog.
	/// </summary>
	public class TrainControllerDialog : Form
	{
		public TrainControllerDialog() {
			InitializeComponent();
			reset();
			World.onNewWorld += new EventHandler(onNewWorld);
		}

		protected override void Dispose( bool disposing ) {
			if( disposing && components != null)
				components.Dispose();
			base.Dispose( disposing );
			World.onNewWorld -= new EventHandler(onNewWorld);
		}

		private void onNewWorld( object sender, EventArgs ea ) {
			reset();
		}

		private void reset() {
			// refresh the list
			list.BeginUpdate();
			list.Items.Clear();
			foreach( TrainController tc in World.world.trainControllers ) {
				if( tc.contribution==null)	continue;	// those are system controllers
				list.Items.Add( createListViewItem(tc) );
			}
			list.EndUpdate();
			list_SelectedIndexChanged(null,null);	// update buttons
		}

		#region Windows Form Designer generated code
		private System.Windows.Forms.ContextMenu newTCMenu;
		private System.Windows.Forms.ListView list;
		private System.Windows.Forms.ColumnHeader columnName;
		private System.Windows.Forms.ColumnHeader columnType;
		private System.Windows.Forms.Panel panel1;
		private System.Windows.Forms.Button buttonNew;
		private System.Windows.Forms.Button buttonConfig;
		private System.Windows.Forms.Button buttonDelete;
		private System.Windows.Forms.Button rename;
        private Button buttonCopy;
		private System.ComponentModel.Container components = null;

		private void InitializeComponent()
		{
            this.panel1 = new System.Windows.Forms.Panel();
            this.rename = new System.Windows.Forms.Button();
            this.buttonDelete = new System.Windows.Forms.Button();
            this.buttonConfig = new System.Windows.Forms.Button();
            this.buttonNew = new System.Windows.Forms.Button();
            this.newTCMenu = new System.Windows.Forms.ContextMenu();
            this.list = new System.Windows.Forms.ListView();
            this.columnName = new System.Windows.Forms.ColumnHeader();
            this.columnType = new System.Windows.Forms.ColumnHeader();
            this.buttonCopy = new System.Windows.Forms.Button();
            this.panel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // panel1
            // 
            this.panel1.BackColor = System.Drawing.Color.Transparent;
            this.panel1.Controls.Add(this.buttonCopy);
            this.panel1.Controls.Add(this.rename);
            this.panel1.Controls.Add(this.buttonDelete);
            this.panel1.Controls.Add(this.buttonConfig);
            this.panel1.Controls.Add(this.buttonNew);
            this.panel1.Dock = System.Windows.Forms.DockStyle.Right;
            this.panel1.Location = new System.Drawing.Point(233, 0);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(105, 174);
            this.panel1.TabIndex = 0;
            // 
            // rename
            // 
            this.rename.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.rename.Location = new System.Drawing.Point(8, 136);
            this.rename.Name = "rename";
            this.rename.Size = new System.Drawing.Size(88, 24);
            this.rename.TabIndex = 5;
            this.rename.Text = "名前変更(&R)";
            this.rename.Click += new System.EventHandler(this.rename_Click);
            // 
            // buttonDelete
            // 
            this.buttonDelete.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.buttonDelete.Location = new System.Drawing.Point(8, 72);
            this.buttonDelete.Name = "buttonDelete";
            this.buttonDelete.Size = new System.Drawing.Size(88, 24);
            this.buttonDelete.TabIndex = 4;
            this.buttonDelete.Text = "削除(&D)";
            this.buttonDelete.Click += new System.EventHandler(this.buttonDelete_Click);
            // 
            // buttonConfig
            // 
            this.buttonConfig.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.buttonConfig.Location = new System.Drawing.Point(8, 40);
            this.buttonConfig.Name = "buttonConfig";
            this.buttonConfig.Size = new System.Drawing.Size(88, 24);
            this.buttonConfig.TabIndex = 3;
            this.buttonConfig.Text = "設定(&S)";
            this.buttonConfig.Click += new System.EventHandler(this.buttonConfig_Click);
            // 
            // buttonNew
            // 
            this.buttonNew.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.buttonNew.Location = new System.Drawing.Point(8, 8);
            this.buttonNew.Name = "buttonNew";
            this.buttonNew.Size = new System.Drawing.Size(88, 24);
            this.buttonNew.TabIndex = 2;
            this.buttonNew.Text = "新規(&N)...";
            this.buttonNew.Click += new System.EventHandler(this.buttonNew_Click);
            // 
            // list
            // 
            this.list.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.columnName,
            this.columnType});
            this.list.Dock = System.Windows.Forms.DockStyle.Fill;
            this.list.FullRowSelect = true;
            this.list.HideSelection = false;
            this.list.LabelEdit = true;
            this.list.Location = new System.Drawing.Point(0, 0);
            this.list.MultiSelect = false;
            this.list.Name = "list";
            this.list.Size = new System.Drawing.Size(233, 174);
            this.list.TabIndex = 1;
            this.list.UseCompatibleStateImageBehavior = false;
            this.list.View = System.Windows.Forms.View.Details;
            this.list.AfterLabelEdit += new System.Windows.Forms.LabelEditEventHandler(this.list_AfterLabelEdit);
            this.list.SelectedIndexChanged += new System.EventHandler(this.list_SelectedIndexChanged);
            this.list.DoubleClick += new System.EventHandler(this.onListDoubleClick);
            // 
            // columnName
            // 
            this.columnName.Text = "名前";
            this.columnName.Width = 120;
            // 
            // columnType
            // 
            this.columnType.Text = "種類";
            this.columnType.Width = 80;
            // 
            // buttonCopy
            // 
            this.buttonCopy.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.buttonCopy.Location = new System.Drawing.Point(8, 104);
            this.buttonCopy.Name = "buttonCopy";
            this.buttonCopy.Size = new System.Drawing.Size(88, 24);
            this.buttonCopy.TabIndex = 6;
            this.buttonCopy.Text = "コピー(&C)";
            this.buttonCopy.Click += new System.EventHandler(this.buttonCopy_Click);
            // 
            // TrainControllerDialog
            // 
            this.AutoScaleBaseSize = new System.Drawing.Size(5, 12);
            this.ClientSize = new System.Drawing.Size(338, 174);
            this.Controls.Add(this.list);
            this.Controls.Add(this.panel1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.SizableToolWindow;
            this.Name = "TrainControllerDialog";
            this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Show;
            this.Text = "ダイヤグラム";
            this.panel1.ResumeLayout(false);
            this.ResumeLayout(false);

		}
		#endregion

		/// <summary> Selected train controller, or null if none is selected. </summary>
		private TrainController selectedItem {
			get {
				if( list.SelectedIndices.Count==0 )	return null;
				
				Debug.Assert( list.SelectedIndices.Count==1 );
				return (TrainController)list.SelectedItems[0].Tag;
			}
		}

		private void buttonNew_Click(object sender, EventArgs e) {
			// reset menu items
			newTCMenu.MenuItems.Clear();
			foreach( TrainControllerContribution contrib in Core.plugins.trainControllers ) {
				MenuItem mi = new MenuItem(contrib.name);
				mi.Click += new EventHandler(new TCCreator(this,contrib).onMenuSelected);
				newTCMenu.MenuItems.Add(mi);
			}

			newTCMenu.Show( buttonNew, new Point(0,buttonNew.Height) );
		}

		/// <summary>
		/// Receives the selection from the context menu.
		/// </summary>
		internal class TCCreator {
			internal TCCreator(
				TrainControllerDialog owner,
				TrainControllerContribution contrib ) {
				this.owner = owner;
				this.contrib = contrib;
			}
			private TrainControllerDialog owner;
			private TrainControllerContribution contrib;
			internal void onMenuSelected( object sender, EventArgs e ) {
				owner.createNewTrainController(contrib);
			}
		}

		/// <summary>
		/// Creates a new train controller.
		/// </summary>
		private void createNewTrainController( TrainControllerContribution contrib ) {
			// update data structure
			TrainController tc =  contrib.newController(
				string.Format("新しいダイヤグラム{0}",iota++));
			World.world.trainControllers.add(tc);

			// update GUI
			list.Items.Add( createListViewItem(tc) );
		}

		/// <summary> sequence number generator. </summary>
		private static int iota=1;

		private ListViewItem createListViewItem( TrainController tc ) {
			ListViewItem lvi = new ListViewItem(new string[]{
				tc.name, tc.contribution.name });
			lvi.Tag = tc;
			return lvi;
		}

		private void buttonConfig_Click(object sender, EventArgs e) {
			this.selectedItem.config(this);
		}

		private void buttonDelete_Click(object sender, EventArgs e) {
			// update data structure
			// TODO: what will happen to trains that are controlled by this train controller?
			World.world.trainControllers.remove(selectedItem);
			
			// update GUI
			list.Items.Remove( list.SelectedItems[0] );
		}

		private void buttonOK_Click(object sender, System.EventArgs e) {
			Close();
		}

		private void list_SelectedIndexChanged(object sender, System.EventArgs e) {
			bool b = selectedItem!=null;
			buttonConfig.Enabled = b;
			buttonDelete.Enabled = b;
			rename.Enabled = b;
		}

		// label was edited. Update the name accordingly
		private void list_AfterLabelEdit(object sender, LabelEditEventArgs e) {
			// it seems like a bug of WinForms, but when the edit is canceled,
			// the empty string is passed.
			if( e.Label==null )	return;	// so treat this case as being cancelled.

			ListViewItem lvi = list.Items[e.Item];
			TrainController tc = (TrainController)lvi.Tag;
			tc.name = e.Label;
		}

		private void rename_Click(object sender, System.EventArgs e) {
			if( list.SelectedIndices.Count==0 )	return;
			list.SelectedItems[0].BeginEdit();
		}

		private void onListDoubleClick(object sender, System.EventArgs e) {
			if( selectedItem!=null )
				buttonConfig_Click(sender,e);
		}


        /// <summary>
        /// ダイヤグラムコピーボタンクリックイベント
        /// </summary>
        /// <remarks>
        /// ダイヤグラムコピーボタンをクリックした時の処理を行う
        /// コピー元のトレインコントローラーを元に新しいトレインコントローラーを生成して、リストビューに追加する
        /// </remarks>
        /// <param name="sender">
        /// イベント発生元オブジェクト
        /// </param>
        /// <param name="e">
        /// イベントパラメータ
        /// </param>
        /// <permission cref="System.Security.PermissionSet">
        /// クラス内のみアクセス可能
        /// </permission>
        private void buttonCopy_Click(object sender, EventArgs e) {
			TrainController newtc = null;                                       // トレインコントローラー

            if (list.SelectedIndices.Count==0) {                                // アイテムが選択されていない場合
                return;                                                         // 何もしない
            }

            foreach (TrainController tc in World.world.trainControllers) {      // トレインコントローラー分、繰り返し
                if (selectedItem.Equals(tc) == true) {                          // 選択しているトレインコントローラーと同一の場合
                                                                                // 選択しているトレインコントローラーと同じオブジェクトを生成
                    newtc = selectedItem.contribution.newController(string.Format("新しいダイヤグラム{0}",iota++));

                    using (MemoryStream ms = new MemoryStream()) {              // メモリーストリームの生成
                        BinaryFormatter formatter = new BinaryFormatter();      // バイナリフォーマッターの生成
                        formatter.Serialize(ms, selectedItem);                  // 選択しているダイヤグラムをメモリーストリームにシリアライズ
                        ms.Position = 0;                                        // メモリーストリームの位置を先頭に戻す
                        newtc = (TrainController)formatter.Deserialize(ms);     // メモリーストリームから新しいトレインコントローラーにデシリアライズする
                    }

                    string originalName = selectedItem.name;
                    string newName = "";
                    int incNo = 1;
                    
                    do {                                                                        // 明示的に指定しない限りは永久ループさせる
    			        newName = string.Format("{0} {1}", originalName, incNo);                // 仮の新しい名前を決定

    				    foreach (TrainController ttc in World.world.trainControllers) {         // ダイヤグラム分、繰り返し
    					    if (ttc.name == newName) {                                          // 同一の名前がある場合
    						    incNo++;                                                        // 末尾の数字 + 1
                                newName = "";                                                   // 初期化することでまだ新しい名前を決定していないというフラグ代わりに使用する
                                break;                                                          // foreach文を抜ける
                            }
						}

                        if (newName != "") {                                                    // 新しい名前が設定されている場合
                            break;                                                              // do文を抜ける
                        }
                    } while (true);

				    newtc.name = newName;                                                       // 新しい名前を設定

                    World.world.trainControllers.add(newtc);                    // 新しいトレインコントローラーを追加
        			list.Items.Add(createListViewItem(newtc));                  // リストビューに新しいトレインコントローラーを追加

                    break;                                                      // ループ終了
                }
            }
        }
	}
}
