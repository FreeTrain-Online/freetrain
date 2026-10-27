using System;
using System.Xml;
using System.Windows.Forms;
using freetrain.framework;
using freetrain.contributions.others;
using freetrain.world.rail;


namespace freetrain.world.rail.speedlimit
{
	/// <summary>
	/// メインウィンドウのメニューに「速度制限」を追加する。　(鉄道)→(速度制限)　　　　
	/// </summary>
	public class MenuContributionImpl : MenuContribution
	{
		/// <summary>
		/// コンストラクタ
		/// </summary>
		public MenuContributionImpl( XmlElement e ) : base(e) {
            SpeedLimit.init();
		}
		

		/// <summary>
		/// メニューを結合させる
		/// </summary>
		public override void mergeMenu( MainMenu containerMenu ) {

			MenuItem item = new MenuItem("速度制限");

			
			MenuItem subItem0 = new MenuItem("速度制限区間");
			MenuItem subItem1 = new MenuItem("速度制限区間（勾配）");
			MenuItem subItem2 = new MenuItem("鉄道上以外の速度制限削除");
			item.MenuItems.AddRange(new MenuItem[] { subItem0, subItem1, subItem2 });
			
			
			subItem0.Click += new System.EventHandler(onClick0);
			subItem1.Click += new System.EventHandler(onClick1);
			subItem2.Click += new System.EventHandler(onClick2);

			containerMenu.MenuItems[2].MenuItems.Add(6,item);

		}

		/// <summary>
		/// 
		/// </summary>
		private void onClick0( object sender, EventArgs args ) 
		{
			//速度制限（平面）
			freetrain.world.rail.speedlimit.SpeedLimitRailRoadController.create();
		}

		/// <summary>
		/// 
		/// </summary>
		private void onClick1( object sender, EventArgs args ) 
		{
			//速度制限（勾配）
            freetrain.world.rail.speedlimit.SpeedLimitSlopeController.create();
		}

		/// <summary>
		/// 
		/// </summary>
		private void onClick2( object sender, EventArgs args ) 
		{
			//鉄道上にない速度制限を削除
            freetrain.world.rail.speedlimit.SpeedLimitManager.removeNotTrafficVoxel();
		}
	}
}
