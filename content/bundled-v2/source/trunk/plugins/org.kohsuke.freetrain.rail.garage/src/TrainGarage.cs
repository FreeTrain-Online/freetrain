// 2009.05.10 Yz 同一ボクセル内に列車がある場合、機関庫を撤去不可能に変更 
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
using freetrain.world.terrain;
using freetrain.world.accounting;

namespace freetrain.world.rail.garage
{
	/// <summary>
	/// SpecialRailContribution implementation for the BridgeRail
	/// </summary>
	[Serializable]
	public class TrainGarageContributionImpl : SpecialRailContribution {
		public TrainGarageContributionImpl(XmlElement e) : base(e) {}





		// static initializer
		protected override void onInitComplete() {
			Picture picture = loadPicture("garage.bmp");
			for( int i=0; i<2; i++ ) {
				backgrounds[i] = new SimpleSprite( picture, new Point(0,11), new Point(32*i   , 0), new Size(32,27) );
				foregrounds[i] = new SimpleSprite( picture, new Point(0,11), new Point(32*i+64, 0), new Size(32,27) );
			}
		}

		// sprites
		private static readonly Sprite[] foregrounds = new Sprite[2];
		private static readonly Sprite[] backgrounds = new Sprite[2];






		/// <summary>
		/// Garage rail roads.
		/// </summary>
		[Serializable]
		internal class GarageRail : SpecialPurposeRailRoad
		{
			internal GarageRail( TrafficVoxel tv, Direction d ) : base(tv,d) {
				this.pictureIndex = (byte)(d.isParallelToX?0:1);
			}

			/// <summary>
			/// Replace this garage by a normal rail road
			/// </summary>
			internal void remove() {
                if (this.voxel.car != null) {                                   // 同一ボクセル内に列車がいる場合
                    return;                                                     // 撤去不可のため、処理終了
                }            
                
                new SingleRailRoad( voxel, RailPattern.get(dir1,dir2));
			}


			//
			// drawing
			//
			private readonly byte pictureIndex;

			public override void drawBefore( DrawContext display, Point pt ) {
				backgrounds[pictureIndex].draw(display.surface,pt);
				// don't call the base class so that we won't draw the rail road unnecessarily
			}
			public override void drawAfter( DrawContext display, Point pt ) {
				foregrounds[pictureIndex].draw(display.surface,pt);
			}

		}





		public override bool canBeBuilt( Location from, Location to ) {
			if( from==to )	return false;

			Debug.Assert( from.z==to.z );

			Direction d = from.getDirectionTo(to);

			Location here = from;

			// there must be at least one water between two locations
			while(true) {
				if( World.world.getGroundLevel(here)!=here.z )
					return false;		// cannot be built above or below the ground

				if( World.world[here]!=null ) {
					TrafficVoxel v = TrafficVoxel.get(here);
					if(v==null)				return false;	// occupied
					if(v.railRoad==null)	return false;	// occupied by something other than RR

					if(!v.railRoad.hasRail(d) || !v.railRoad.hasRail(d.opposite))
						return false;	// rail is going to some other directions
				}

				if( here==to )	return true;	// all OK
				here = here.toward(to);
			}
		}



		public override void build( Location here, Location to ) {
			Debug.Assert( canBeBuilt(here,to) );

			Direction d = here.getDirectionTo(to);
            long cost = 0;

			while(true) {
				new GarageRail( TrafficVoxel.getOrCreate(here), d );
                cost += GARAGERAILROAD_CONSTRUCTION_UNIT_COST;

				if( here==to )	break;
				here = here.toward(to);
			}
            AccountGenre.RAIL_SERVICE.spend( cost );
		}


		public override void remove( Location here, Location to ) {
			if( here==to )	return;

			Direction d = here.getDirectionTo(to);
            long cost = 0;

			for( ; here!=to; here = here.toward(to) ) {
				GarageRail grr = RailRoad.get(here) as GarageRail;
                if( grr!=null && grr.hasRail(d) ){
					grr.remove();	// destroy it
                    cost += GARAGERAILROAD_DESTRUCTION_UNIT_COST;
                }

            AccountGenre.RAIL_SERVICE.spend( cost );
			}
		}
/*
		public override long calcCostOfBuild( Location here, Location to ) {
			if( here == to ) return 0;

			Direction d = here.getDirectionTo(to);
            long cost = 0;

			while(true) {
				if( here==to )	break;
				here = here.toward(to);
                cost += GARAGERAILROAD_CONSTRUCTION_UNIT_COST;
			}
            return cost;
		}


		public override long calcCostOfRemove( Location here, Location to ) {
			if( here==to )	return 0;

			Direction d = here.getDirectionTo(to);
            long cost = 0;

			for( ; here!=to; here = here.toward(to) ) {
                    cost += GARAGERAILROAD_DESTRUCTION_UNIT_COST;
                }

            return cost;
			
		}
*/

		public override string name { get { return "機関庫"; } }

		public override string oneLineDescription { get { return "列車を保管・整備・点検する施設。ただの飾り"; } }
	
		public override Bitmap previewBitmap {
			get {
				using( PreviewDrawer d = new PreviewDrawer( new Size(100,100), new Size(5,1), 0 ) ) {
					for( int i=5; i>=2; i-- ) {
						d.draw( backgrounds[0], i, 0 );
						d.draw( foregrounds[0], i, 0 );
					}
					for( int i=1; i>=-5; i-- ) {
						d.draw( RailPattern.get( Direction.EAST, Direction.WEST ), i, 0 );
					}
					return d.createBitmap();
				}
			}
		}

		private const int GARAGERAILROAD_DESTRUCTION_UNIT_COST  = 200000;
		private const int GARAGERAILROAD_CONSTRUCTION_UNIT_COST = 6500000;


    }
}
