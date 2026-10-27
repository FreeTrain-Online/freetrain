// 2009.05.10 Yz 同一ボクセル内に列車がある場合、トンネルを撤去不可能に変更
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

namespace freetrain.world.rail
{
	/// <summary>
	/// SpecialRailContribution implementation for the BridgeRail
	/// </summary>
	[Serializable]
	public class TunnelRailContributionImpl : SpecialRailContribution {
		public TunnelRailContributionImpl(XmlElement e) : base(e) {}





		// static initializer
		protected internal override void onInitComplete() {
			Picture picture = loadPicture("TunnelRail.bmp");
			for( int i=0; i<2; i++ ) {
				backgrounds[i] = new SimpleSprite( picture, new Point(0,16), new Point(32*i   , 0), new Size(32,32) );
				foregrounds[i] = new SimpleSprite( picture, new Point(0,16), new Point(32*i+64, 0), new Size(32,32) );
			}
		}

		// sprites
		private static readonly Sprite[] foregrounds = new Sprite[2];
		private static readonly Sprite[] backgrounds = new Sprite[2];






		/// <summary>
		/// Tunnel rail roads.
		/// </summary>
		[Serializable]
		internal class TunnelRail : SpecialPurposeRailRoad
		{
			internal TunnelRail( TrafficVoxel tv, Direction d, byte pictIdx, byte[] _heights ) : base(tv,d) {
				this.pictureIndex = pictIdx;
				this.heights = _heights;

				if(d.index<4)	sOrW=d.opposite;
				else			sOrW=d;
			}

			/// <summary>
			/// stores corner heights of the mountain voxel so that we can restore it
			/// when this tunnel is removed.
			/// </summary>
			private readonly byte[] heights;

			/// <summary>
			/// this.dir1==sOrW || this.dir2==sOrW;
			/// and
			/// sOrW==Direction.SOUTH || sOrW==Direction.WEST;
			/// </summary>
			private readonly Direction sOrW;

			/// <summary>
			/// Removes this tunnel rail road and restore the original mountain voxel
			/// </summary>
			internal void remove() {
				Location loc = this.location;
				World.world.remove(loc);
				new MountainVoxel( loc, heights[0],  heights[1],  heights[2],  heights[3] );
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

			public override void invalidateVoxel() {
				if( sOrW==null || !(RailRoad.get(this.location+sOrW) is TunnelRail) )
					World.world.onVoxelUpdated(this.location);
				
				// otherwise no need to update the voxel since a train will be hidden by this tunnel
			}
		}





		public override bool canBeBuilt( Location from, Location to ) {
			if( from==to )	return false;
			if( from.z < World.world.waterLevel )	return false;	// below the water level

			Debug.Assert( from.z==to.z );

			Direction d = from.getDirectionTo(to);

			Location here = from;
			bool atLeastOneMountain=false;

			// there must be at least one water between two locations
			while(true) {
				if( World.world[here]!=null ) {
					if((World.world[here] as MountainVoxel)!=null) {
						atLeastOneMountain = true;
					} else {
						TrafficVoxel v = TrafficVoxel.get(here);
						if(v==null)				return false;	// occupied
						if(v.railRoad==null)	return false;	// occupied by something other than RR

						if(!v.railRoad.hasRail(d) || !v.railRoad.hasRail(d.opposite))
							return false;	// rail is running 
					}
				}

				if( here==to )	return atLeastOneMountain;
				here = here.toward(to);
			}
		}





		public override void build( Location here, Location to ) {
			Debug.Assert( canBeBuilt(here,to) );

			Direction d = here.getDirectionTo(to);
            long cost = 0;
          
			while(true) {
				if( RailRoad.get(here)==null ) {
					MountainVoxel mv = World.world[here] as MountainVoxel;
					if( mv!=null ) {
						// build a tunnel
						byte[] heights = new byte[4];
						for( int i=0; i<4; i++ )
							heights[i] = (byte)mv.getHeight(Direction.get(i*2+1));

						World.world.remove(here);	// remove this mountain

                        cost += RAILROAD_CONSTRUCTION_UNIT_COST * 2;
						create( TrafficVoxel.getOrCreate(here), d, heights );
					} else {
						// build a normal tunnel
						new SingleRailRoad( TrafficVoxel.getOrCreate(here), RailPattern.get( d, d.opposite ) );
                        cost += RAILROAD_CONSTRUCTION_UNIT_COST * 2;
					}
				}
				if( here==to )	break;
				here = here.toward(to);
			}
            AccountGenre.RAIL_SERVICE.spend( cost );
		}

		private void create( TrafficVoxel v, Direction d, byte[] heights ) {
			Debug.Assert( d.isSharp );

			if( d.isParallelToY )
				new TunnelRail( v, d, 1, heights );
			else
				new TunnelRail( v, d, 0, heights );
		}






		public override void remove( Location here, Location to ) {
			TrafficVoxel v = null;

            if( here==to )	return;

			Direction d = here.getDirectionTo(to);
            long cost = 0;

			for( ; here!=to; here = here.toward(to) ) {
				TunnelRail trr = RailRoad.get(here) as TunnelRail;
				if( trr!=null && trr.hasRail(d) ) {
                    v = World.world[here] as TrafficVoxel;                      // 現在対象の位置のボクセルを取得
                    if (v != null) {                                            // ボクセルがトラフィックボクセルの場合
                        if (v.car != null) {                                    // 該当ボクセルに車が存在する場合
                            if (v.car is Train.TrainCar) {                      // 車が列車の場合
                                continue;
                            }
                        }
                    }
                    
                    cost += RAILROAD_DESTRUCTION_UNIT_COST *2;
                    trr.remove();	// destroy it
                }
			}
            AccountGenre.RAIL_SERVICE.spend( cost );
		}


/*
		public override long calcCostOfBuild( Location here, Location to ) {
			if( here == to ) return 0;

			Direction d = here.getDirectionTo(to);
            long cost = 0;

			while(true) {
				if( here==to )	break;
				here = here.toward(to);
                cost += RAILROAD_CONSTRUCTION_UNIT_COST * 2;
			}
            return cost;
		}


		public override long calcCostOfRemove( Location here, Location to ) {
			if( here==to )	return 0;

			Direction d = here.getDirectionTo(to);
            long cost = 0;

			for( ; here!=to; here = here.toward(to) ) {
                    cost += RAILROAD_DESTRUCTION_UNIT_COST *2;
                }

            return cost;
			
		}
*/



		public override string name { get { return "トンネル"; } }

		public override string oneLineDescription { get { return "山肌を突き抜けるためのトンネル"; } }
	
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
		private const int RAILROAD_DESTRUCTION_UNIT_COST  = 2000000;
		private const int RAILROAD_CONSTRUCTION_UNIT_COST = 6000000;
	}
}
