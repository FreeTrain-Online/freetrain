// 2010.05.10 riorio Add cost compute
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Xml;
using freetrain.contributions.rail;
using freetrain.framework;
using freetrain.framework.graphics;
using freetrain.framework.plugin;
using freetrain.util;
using freetrain.world.accounting;

namespace freetrain.world.rail
{
	/// <summary>
	/// SpecialRailContribution implementation for the steal-supported rail
	/// </summary>
	[Serializable]
	public class StealSupportedRailContributionImpl : SpecialRailContribution {
		public StealSupportedRailContributionImpl(XmlElement e) : base(e) {}



		protected internal override void onInitComplete() {
			Picture picture = loadPicture("StealSupportedRail.bmp");
			sprites[0] = new SimpleSprite( picture, new Point(0,16), new Point( 0, 0), new Size(32,32) );
			sprites[1] = new SimpleSprite( picture, new Point(0,16), new Point(32, 0), new Size(32,32) );
			sprites[2] = new SimpleSprite( picture, new Point(0,16), new Point(64, 0), new Size(32,32) );
			sprites[3] = new SimpleSprite( picture, new Point(0,16), new Point(96, 0), new Size(32,32) );
		}

		// sprites
		private static readonly Sprite[] sprites = new Sprite[4];





		[Serializable]
		internal class RailImpl : SpecialPurposeRailRoad
		{
			internal RailImpl( TrafficVoxel tv, Direction d ) : base(tv,d) {}

			public override void drawBefore( DrawContext display, Point pt ) {
				Sprite s=null;
				switch(dir1.index) {
				case 0: case 4:		s = sprites[3]; break;
				case 1: case 5:		s = sprites[1]; break;
				case 2: case 6:		s = sprites[0]; break;
				case 3: case 7:		s = sprites[2]; break;
				}

				s.draw(display.surface,pt);
				// don't call the base class so that we won't draw the rail road unnecessarily
			}
		}






		public override bool canBeBuilt( Location from, Location to ) {
			if( from==to )	return false;

			Direction d = from.getDirectionTo(to);

			Location here = from;

			while(true) {
				if( World.world[here]!=null ) {
					TrafficVoxel v = TrafficVoxel.get(here);
					if(v==null)				return false;	// occupied
					if(v.railRoad==null)	return false;	// occupied by something other than RR

					if(!v.railRoad.hasRail(d) || !v.railRoad.hasRail(d.opposite))
						return false;	// rail is running 
				}

				if( World.world.getGroundLevel(here) >= here.z )
					return false;	// must be all raised 

				if( here==to )	return true;
				here = here.toward(to);
			}
		}




		public override void build( Location here, Location to ) {
			Debug.Assert( canBeBuilt(here,to) );

			Direction d = here.getDirectionTo(to);

            AccountGenre.RAIL_SERVICE.spend( calcBuildBridgeRailRoadCost( here, to ));

            while(true) {
				if( RailRoad.get(here)==null ) {
					TrafficVoxel tv = TrafficVoxel.getOrCreate(here);
					new RailImpl( tv, d );
					BridgePierVoxel.electBridgeSupport( here, tv );
				}

				if( here==to )	return;
				here = here.toward(to);
			}
		}




		public override void remove( Location here, Location to ) {
			TrafficVoxel v = null;
			if( here==to )	return;

			Direction d = here.getDirectionTo(to);

            AccountGenre.RAIL_SERVICE.spend( calcDestroyBridgeRailRoadCost( here, to ));

            while(true) {
				RailImpl rr = RailRoad.get(here) as RailImpl;
				if( rr!=null && rr.hasRail(d) ) {
                    v = World.world[here] as TrafficVoxel;                      // 現在対象の位置のボクセルを取得
                    if (v != null) {                                            // ボクセルがトラフィックボクセルの場合
                        if (v.car != null) {                                    // 該当ボクセルに車が存在する場合
                            if (v.car is Train.TrainCar) {                      // 車が列車の場合
                                goto common;                                    // 処理を抜ける
                            } 
                        }
                    }
					// destroy it
					rr.voxel.railRoad = null;
					// TODO: delete piers

					BridgePierVoxel.teardownBridgeSupport( here,TrafficVoxel.get(here) );
				}
            common:
				if(here==to)	return;
				here = here.toward(to);
			}
		}

		/// <summary>
		/// Compute the cost of building bridge rail road 
		/// </summary>
		private static long calcBuildBridgeRailRoadCost( Location from, Location to ) {
			int waterLevel = World.world.waterLevel;
            long cost = 0;
            int height, multiplier;
            int glevel = World.world.getGroundLevel(from);
			if( glevel<=from.z && from.z<=waterLevel && glevel<waterLevel )
				return 0;	// underwater or on water.

            while(true) {
                glevel = World.world.getGroundLevel(from);
			    height = from.z - glevel;
                multiplier = ( height < 0 )? height * -2 : height + 1;

                cost += RAILROAD_CONSTRUCTION_UNIT_COST*multiplier;

				if( from==to )	break;
				from = from.toward(to);
			}

			return cost;
		}

		/// <summary>
		/// Compute the cost of building bridge rail road 
		/// </summary>
		private static long calcDestroyBridgeRailRoadCost( Location from, Location to ) {
			int waterLevel = World.world.waterLevel;
            long cost= 0;
            int height, multiplier;
            int glevel = World.world.getGroundLevel(from);
			if( glevel<=from.z && from.z<=waterLevel && glevel<waterLevel )
				return 0;	// underwater or on water.

            while(true) {
                glevel = World.world.getGroundLevel(from);
			    height = from.z - glevel;
                multiplier = ( height < 0 )? height * -2 : height + 1;

                cost += RAILROAD_DESTRUCTION_UNIT_COST*multiplier;

				if( from==to )	break;
				from = from.toward(to);
			}

			return cost;
		}

        private const int RAILROAD_DESTRUCTION_UNIT_COST  = 2000000;
		private const int RAILROAD_CONSTRUCTION_UNIT_COST = 6000000;


		public override string name { get { return "ガード高架"; } }

		public override string oneLineDescription { get { return "スチールで強化された高架"; } }

		public override DirectionMode directionMode { get { return DirectionMode.EightWay; } }

		public override Bitmap previewBitmap {
			get {
				using( PreviewDrawer d = new PreviewDrawer( new Size(100,100), new Size(5,1), 0 ) ) {
					for( int i=6; i>=-2; i-- ) {
						d.draw( BridgePierVoxel.defaultSprite, i, 0 );
						d.draw( sprites[0], i+1, -1 );
					}

					return d.createBitmap();
				}
			}
		}
	}
}
