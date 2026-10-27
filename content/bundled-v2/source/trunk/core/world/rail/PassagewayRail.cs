// 2009.05.17 RioRio âwòAóçí òHÇÃéÌóﬁÇëIëÇ≈Ç´ÇÈÇÊÇ§Ç…ïœçX
using System;
using System.Diagnostics;
using System.Drawing;
using freetrain.framework;
using freetrain.framework.graphics;

namespace freetrain.world.rail
{
	/// <summary>
	/// Rail road with a raised passageway
	/// </summary>
	[Serializable]
	public class PassagewayRail : SpecialPurposeRailRoad
	{
		public PassagewayRail( TrafficVoxel v, Direction dir ) : base(v,dir) {
			Debug.Assert( dir.isSharp );
		}

		public void drawAfter( DrawContext display, Point pt, ThinPlatform.PassagewayRailType passagewayRailType ) {
			getFloatingSprite(dir1, passagewayRailType).draw( display.surface, pt );
		}




		/// <summary>
		/// sprites for passageways.
		/// 0 : single-width north platform and bridge connecting to east
		/// 1:  double-width north platform
		/// 2:  double-width north platform and bridge connecting to east
		/// 
		/// 3-5: east
		/// 
		/// 6-8: south
		/// 
		/// 9-11: west
		/// 
		/// 12: E-W bridge
		/// 13: N-S bridge
		/// </summary>
		private static readonly Sprite[,] sprites;

		/// <summary>
		/// 
		/// </summary>
		/// <param name="d">direction of the rail road</param>
		/// <param name="connected">true if a passageway is bridged</param>
		/// <param name="doubleWidth">true if a platform is double-width</param>
		/// <returns></returns>
		public static Sprite getSprite( Direction d, bool bridged, bool doubleWidth, ThinPlatform.PassagewayRailType passagewayRailType ) {
            switch (passagewayRailType) {
                case ThinPlatform.PassagewayRailType.pwrtConcrete:
                    return sprites[0, (d.index/2)*3+(doubleWidth?(bridged?2:1):0)];	// TODO
                case ThinPlatform.PassagewayRailType.pwrtWoodenBrown:
                    return sprites[1, (d.index/2)*3+(doubleWidth?(bridged?2:1):0)];	// TODO
                case ThinPlatform.PassagewayRailType.pwrtWoodenGreen:
                    return sprites[2, (d.index/2)*3+(doubleWidth?(bridged?2:1):0)];	// TODO
                default:
                    return sprites[0, (d.index/2)*3+(doubleWidth?(bridged?2:1):0)];	// TODO
            }
		}
		public static Sprite getFloatingSprite( Direction d, ThinPlatform.PassagewayRailType passagewayRailType ) {
			if (d.isParallelToX) {
                switch (passagewayRailType) {
                    case ThinPlatform.PassagewayRailType.pwrtConcrete:
                        return sprites[0, 12];
                    case ThinPlatform.PassagewayRailType.pwrtWoodenBrown:
                        return sprites[1, 12];
                    case ThinPlatform.PassagewayRailType.pwrtWoodenGreen:
                        return sprites[2, 12];
                    default:
                        return sprites[0, 12];
                }
            } else {
                switch (passagewayRailType) {
                    case ThinPlatform.PassagewayRailType.pwrtConcrete:
                        return sprites[0, 13];
                    case ThinPlatform.PassagewayRailType.pwrtWoodenBrown:
                        return sprites[1, 13];
                    case ThinPlatform.PassagewayRailType.pwrtWoodenGreen:
                        return sprites[2, 13];
                    default:
                        return sprites[0, 13];
                }
            }
		}



		/// <summary>
		/// Sprites for stair cases.
		/// 
		/// 8 spirtes for one direction.
		/// (upward --- stairs go upward to the direction of the platform)
		/// 0: single-width, no-roof
		/// 1: single-width, roof
		/// 2: double-width, no-roof
		/// 3: double-width, roof
		/// (downward -- stairs go downward to the direction of the platform)
		/// 4,5,6,7
		/// </summary>
		private static readonly Sprite[,] stairSprites;

		public static Sprite getStairSprite( Direction d, bool upward, bool hasRoof, bool doubleWidth, ThinPlatform.PassagewayRailType passagewayRailType ) {
            switch (passagewayRailType) {
                case ThinPlatform.PassagewayRailType.pwrtConcrete:
        			return stairSprites[0, d.index*4 | (upward?0:4) | (doubleWidth?2:0) | (hasRoof?1:0)];	// TODO
                case ThinPlatform.PassagewayRailType.pwrtWoodenBrown:
        			return stairSprites[1, d.index*4 | (upward?0:4) | (doubleWidth?2:0) | (hasRoof?1:0)];	// TODO
                case ThinPlatform.PassagewayRailType.pwrtWoodenGreen:
        			return stairSprites[2, d.index*4 | (upward?0:4) | (doubleWidth?2:0) | (hasRoof?1:0)];	// TODO
                default:
        			return stairSprites[0, d.index*4 | (upward?0:4) | (doubleWidth?2:0) | (hasRoof?1:0)];	// TODO
            }
		}

        private const int MaxPassagewayRailType = 3;
		static PassagewayRail() {
			Point offset = new Point(0,16);
			Size sz = new Size(32,32);
            Picture[] bmp = new Picture[MaxPassagewayRailType]; 

			sprites = new Sprite[MaxPassagewayRailType, 14];
    	    stairSprites = new Sprite[MaxPassagewayRailType, 32];

            bmp[0] = PictureManager.get("{3197A63A-89DC-4237-8C9B-045F41F31CDB}");
			bmp[1] = PictureManager.get("{3197A63A-89DC-4237-8C9B-045F41F31CDB}-brown");
			bmp[2] = PictureManager.get("{3197A63A-89DC-4237-8C9B-045F41F31CDB}-green");

            for (int j = 0; j < 3; j++) {
                for( int i=0; i<4; i++ ) {
				    sprites[j, i*3  ] = new SimpleSprite( bmp[j], offset, new Point(i*32, 8), sz );
				    sprites[j, i*3+1] = new SimpleSprite( bmp[j], offset, new Point((i*2+1)*32,40), sz );
				    sprites[j, i*3+2] = new SimpleSprite( bmp[j], offset, new Point((i*2+2)*32,40), sz );
			    }
			    sprites[j, 12] = new SimpleSprite( bmp[j], offset, new Point(4*32, 8), sz );
			    sprites[j, 13] = new SimpleSprite( bmp[j], offset, new Point(5*32, 8), sz );

			    // NORTH
			    stairSprites[j,  0] = new SimpleSprite( bmp[j], new Point(+6,16), new Point( 16-6, 80), sz ); 
			    stairSprites[j,  1] = new SimpleSprite( bmp[j], new Point(+6,16), new Point( 48-6, 80), sz );
			    stairSprites[j,  2] = stairSprites[j, 0];	// can reuse the same sprites
			    stairSprites[j,  3] = stairSprites[j, 1];

			    stairSprites[j,  4] = new SimpleSprite( bmp[j], offset, new Point(  0,120), sz ); 
			    stairSprites[j,  5] = new SimpleSprite( bmp[j], offset, new Point( 32,120), sz );
			    stairSprites[j,  6] = stairSprites[j, 4];	// can reuse the same sprites
			    stairSprites[j,  7] = stairSprites[j, 5];

			    // EAST
			    stairSprites[j,  8] = new SimpleSprite( bmp[j], new Point(0,20), new Point( 80, 80-4), new Size(32,36) ); 
			    stairSprites[j,  9] = new SimpleSprite( bmp[j], new Point(0,20), new Point(112, 80-4), new Size(32,36) );
			    stairSprites[j, 10] = new SimpleSprite( bmp[j], new Point(0,20), new Point(192,  8-4), new Size(32,36) );
			    stairSprites[j, 11] = new SimpleSprite( bmp[j], new Point(0,20), new Point(224,  8-4), new Size(32,36) );

			    stairSprites[j, 12] = new SimpleSprite( bmp[j], new Point(+6,16), new Point( 96-6,120), sz ); 
			    stairSprites[j, 13] = new SimpleSprite( bmp[j], new Point(+6,16), new Point(128-6,120), sz );
			    stairSprites[j, 14] = stairSprites[j, 12];	// can reuse the same sprites
			    stairSprites[j, 15] = stairSprites[j, 13];

			    // SOUTH
			    stairSprites[j, 16] = new SimpleSprite( bmp[j], new Point(-6,16), new Point(160+6,120), sz ); 
			    stairSprites[j, 17] = new SimpleSprite( bmp[j], new Point(-6,16), new Point(192+6,120), sz );
			    stairSprites[j, 18] = stairSprites[j, 16];	// can reuse the same sprites
			    stairSprites[j, 19] = stairSprites[j, 17];

			    stairSprites[j, 20] = new SimpleSprite( bmp[j], new Point(0,20), new Point(176, 80-4), new Size(32,36)  ); 
			    stairSprites[j, 21] = new SimpleSprite( bmp[j], new Point(0,20), new Point(208, 80-4), new Size(32,36)  );
			    stairSprites[j, 22] = new SimpleSprite( bmp[j], new Point(0,20), new Point(256,  8-4), new Size(32,36) );
			    stairSprites[j, 23] = new SimpleSprite( bmp[j], new Point(0,20), new Point(388,  8-4), new Size(32,36) );

			    // WEST
			    stairSprites[j, 24] = new SimpleSprite( bmp[j], offset, new Point(256,120), sz ); 
			    stairSprites[j, 25] = new SimpleSprite( bmp[j], offset, new Point(288,120), sz );
			    stairSprites[j, 26] = stairSprites[j, 24];	// can reuse the same sprites
			    stairSprites[j, 27] = stairSprites[j, 25];

			    stairSprites[j, 28] = new SimpleSprite( bmp[j], new Point(-6,16), new Point(240+6, 80), sz ); 
			    stairSprites[j, 29] = new SimpleSprite( bmp[j], new Point(-6,16), new Point(272+6, 80), sz );
			    stairSprites[j, 30] = stairSprites[j, 28];
			    stairSprites[j, 31] = stairSprites[j, 29];
            }
		}
	}
}
