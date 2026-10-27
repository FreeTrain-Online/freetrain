// Original author ƒXƒŒ6 >>565 , 2009/09/25 , ver 1.0.0.0
// Attached by riorio & ƒXƒŒ6 >>820 , 2010/03/21 , ver 1.0.0.3

#include "stdafx.h"
#include "DirectXWrapper.h"


#pragma unmanaged
 

namespace
{
	enum DisplayMode
	{
		RGBMODE_555,
		RGBMODE_565,
		RGBMODE_16,
		RGBMODE_24,
		RGBMODE_32,

		RGBMODE_UNCHECKED,
	};

	DisplayMode dwMode = RGBMODE_UNCHECKED;

	DisplayMode getDisplayMode( IDirectDrawSurface7* lpDDS )
	{
		DDSURFACEDESC2	ddsd;

		//
		// Initialize the surface description structure.
		//
		memset( &ddsd, 0, sizeof ddsd );
		ddsd.dwSize = sizeof ddsd;

		// Get the description of the surface.
		lpDDS->GetSurfaceDesc( &ddsd );

		// If we are in 32 bit mode ...
		if ( ddsd.ddpfPixelFormat.dwRGBBitCount == 32 )
		{
			// ... inform the caller.
			return RGBMODE_32;
		}

		// If we are in 24 bit mode ...
		if ( ddsd.ddpfPixelFormat.dwRGBBitCount == 24 )
		{
			// ... inform the caller.
			return RGBMODE_24;
		}

		// If we are in 16 bit mode ...
		if ( ddsd.ddpfPixelFormat.dwRGBBitCount == 16 )
		{
			// 
			// ... determine the exact mode.
			//

			// If we are in 565 mode ...
			if ( ddsd.ddpfPixelFormat.dwRBitMask == ( 31 << 11 ) &&
				 ddsd.ddpfPixelFormat.dwRBitMask == ( 63 << 5 ) &&
				 ddsd.ddpfPixelFormat.dwRBitMask == 31 )
			{
				// ... inform the caller.
 				return RGBMODE_565;
			}

			// If we are in 555 mode ...
			if ( ddsd.ddpfPixelFormat.dwRBitMask == ( 31 << 10 ) &&
				 ddsd.ddpfPixelFormat.dwRBitMask == ( 31 << 5 ) &&
				 ddsd.ddpfPixelFormat.dwRBitMask == 31 )
			{
				// ... inform the caller.
 				return RGBMODE_555;
			}

			// We got an unknown 16 bit mode.
			return RGBMODE_16;
		}

		// Any other mode must be a palletized one.
		return (DisplayMode)-1;
	}

	HRESULT init( IDirectDrawSurface7* pSurface ) {
          
		dwMode = getDisplayMode(pSurface);
		return (dwMode!=-1)?S_OK:E_UNEXPECTED;
	}

	inline DWORD translateColor( DWORD color, int* srcColors, int* dstColors, int colorsLen ) {
		for( int i=colorsLen-1; i>=0; i-- )
			if( srcColors[i]==color )
				return (DWORD)dstColors[i];
		return color;
	}
}

HRESULT bltAlphaFast(
	IDirectDrawSurface7* lpDDSDest,
	IDirectDrawSurface7* lpDDSSource,
	int iDestX,
	int iDestY,
	int sourceX1, int sourceY1, int sourceX2, int sourceY2,
	DWORD colorKey ) {

	DDSURFACEDESC2	ddsdSource;
	DDSURFACEDESC2	ddsdTarget;
	DWORD			dwTargetPad;
	DWORD			dwSourcePad;
	DWORD			dwTargetTemp;
	DWORD			dwSourceTemp;
	WORD			wMask;
	DWORD			dwDoubleMask;
	BYTE*			lpbTarget;
	BYTE*			lpbSource;
	int				iWidth;
	int				iHeight;
	bool			gOddWidth;
	int				iRet = 0;
	int				i;

	if( dwMode==RGBMODE_UNCHECKED )
		init(lpDDSDest);

	//
	// Get the width and height from the passed rectangle.
	//
	iWidth =  sourceX2-sourceX1;
	iHeight = sourceY2-sourceY1;


	//
	// Lock down the destination surface.
	//
	memset( &ddsdTarget, 0, sizeof ddsdTarget );
	ddsdTarget.dwSize = sizeof ddsdTarget;
	lpDDSDest->Lock( NULL, &ddsdTarget, DDLOCK_WAIT, NULL );  

	//
	// Lock down the source surface.
	//
	memset( &ddsdSource, 0, sizeof ddsdSource );
	ddsdSource.dwSize = sizeof ddsdSource;
	lpDDSSource->Lock( NULL, &ddsdSource, DDLOCK_WAIT, NULL );


/// Now this might be my problem, but ddsdTarget.lpSurface
/// doesn't seem to correctly reflect the lock region.
/// so I modified the code to lock the entire region and adjust lpSurface afterward.

	// clipping
	if(iDestX<0) {
		sourceX1 -= iDestX;
		iWidth += iDestX;
		iDestX=0;
	}
	if(iDestY<0) {
		sourceY1 -= iDestY;
		iHeight += iDestY;
		iDestY=0;
	}
	int extra;
	extra = (iDestX+iWidth)-ddsdTarget.dwWidth;
	if(extra>0) {
		sourceX2 -= extra;
		iWidth -= extra;
	}
	extra = (iDestY+iHeight)-ddsdTarget.dwHeight;
	if(extra>0) {
		sourceY2 -= extra;
		iHeight -= extra;
	}
	if( iWidth<=0 || iHeight<=0 ) {
		lpDDSDest->Unlock( NULL );
		lpDDSSource->Unlock( NULL );
		return true;	// no region to draw
	}

	//
	// Perform the blit operation.
	//
	switch ( dwMode )
	{
	/* 16 bit mode ( 555 ). This algorithm 
	   can process two pixels at once. */
	case RGBMODE_555:
		ddsdTarget.lpSurface = static_cast<BYTE*>(ddsdTarget.lpSurface) + ddsdTarget.lPitch*iDestY + iDestX*2;
		ddsdSource.lpSurface = static_cast<BYTE*>(ddsdSource.lpSurface) + ddsdSource.lPitch*sourceY1 + sourceX1*2;
		//
		// Determine the padding bytes for the target and the source.
		//
		dwTargetPad = ddsdTarget.lPitch - ( iWidth * 2 );
		dwSourcePad = ddsdSource.lPitch - ( iWidth * 2 );

		// If the width is odd ...
		if ( iWidth & 0x01 )
		{
			// ... set the flag ...
			gOddWidth = true;

			// ... and calculate the width.
			iWidth = ( iWidth - 1 ) / 2;
		}
		// If the width is even ...
		else
		{
			// ... clear the flag ...
			gOddWidth = false;

			// ... and calculate the width.
			iWidth /= 2;
		}

		// Get the address of the target.
		lpbTarget = ( BYTE* ) ddsdTarget.lpSurface;

		// Get the address of the source.
		lpbSource = ( BYTE* ) ddsdSource.lpSurface;

		do
		{
			// Reset the width.
			i = iWidth;

			// 
			// Alpha-blend two pixels at once.
			//
			while ( i-- > 0 )
			{
				// Read in two source pixels.
				dwSourceTemp = *( ( DWORD* ) lpbSource );

				// If the two source pixels are not both black ...
				if ( dwSourceTemp != colorKey )
				{
					// ... read in two target pixels.
					dwTargetTemp = *( ( DWORD* ) lpbTarget );

					// If the first source is black ...
					if ( ( dwSourceTemp >> 16 ) == colorKey )
					{
						// ... make sure the first target pixel won´t change.
						dwSourceTemp = (dwSourceTemp&0x0000FFFF) | (dwTargetTemp & 0xffff0000);
					}

					// If the second source is black ...
					if ( ( dwSourceTemp & 0xffff ) == colorKey )
					{
						// ... make sure the second target pixel won´t change.
						dwSourceTemp = (dwTargetTemp&0x0000FFFF) | (dwSourceTemp & 0xffff0000);
					}

					// Calculate the destination pixels.
					dwTargetTemp = ( ( dwTargetTemp & 0x7bde7bde ) >> 1 ) + 
						( ( dwSourceTemp & 0x7bde7bde ) >> 1 );

					// Write the destination pixels.
					*( ( DWORD* ) lpbTarget ) = dwTargetTemp;
				}

				//
				// Proceed to the next two pixels.
				//
				lpbTarget += 4;
				lpbSource += 4;
			}

			//
			// Handle an odd width.
			//
			if ( gOddWidth )
			{
				// Read in one source pixel.
				dwSourceTemp = *( ( WORD* ) lpbSource );

				// If this is not the color key ...
				if ( dwSourceTemp != colorKey )
				{
					//
					// ... apply the alpha blend to it.
					//

					// Read in one target pixel.
					dwTargetTemp = *( ( WORD* ) lpbTarget );

					// Write the destination pixel.
					*( ( WORD* ) lpbTarget ) = ( WORD ) 
						( ( ( dwTargetTemp & 0x7bde ) >> 1 ) + 
						  ( ( dwSourceTemp & 0x7bde ) >> 1 ) );
				}

				// 
				// Proceed to next pixel.
				//
				lpbTarget += 2;
				lpbSource += 2;
			}

			//
			// Proceed to the next line.
			//
			lpbTarget += dwTargetPad;
			lpbSource += dwSourcePad;
		} 
		while ( --iHeight > 0 );

		break;

	/* 16 bit mode ( 565 ). This algorithm 
	   can process two pixels at once. */
	case RGBMODE_565:
		ddsdTarget.lpSurface = static_cast<BYTE*>(ddsdTarget.lpSurface) + ddsdTarget.lPitch*iDestY + iDestX*2;
		ddsdSource.lpSurface = static_cast<BYTE*>(ddsdSource.lpSurface) + ddsdSource.lPitch*sourceY1 + sourceX1*2;
		//
		// Determine the padding bytes for the target and the source.
		//
		dwTargetPad = ddsdTarget.lPitch - ( iWidth * 2 );
		dwSourcePad = ddsdSource.lPitch - ( iWidth * 2 );

		// If the width is odd ...
		if ( iWidth & 0x01 )
		{
			// ... set the flag ...
			gOddWidth = true;

			// ... and calculate the width.
			iWidth = ( iWidth - 1 ) / 2;
		}
		// If the width is even ...
		else
		{
			// ... clear the flag ...
			gOddWidth = false;

			// ... and calculate the width.
			iWidth /= 2;
		}

		// Get the address of the target.
		lpbTarget = ( BYTE* ) ddsdTarget.lpSurface;

		// Get the address of the source.
		lpbSource = ( BYTE* ) ddsdSource.lpSurface;

		do
		{
			// Reset the width.
			i = iWidth;

			// 
			// Alpha-blend two pixels at once.
			//
			while ( i-- > 0 )
			{
				// Read in two source pixels.
				dwSourceTemp = *( ( DWORD* ) lpbSource );

				// If the two source pixels are not both black ...
				if ( dwSourceTemp != colorKey )
				{
					// ... read in two target pixels.
					dwTargetTemp = *( ( DWORD* ) lpbTarget );

					// If the first source is black ...
					if ( ( dwSourceTemp >> 16 ) == colorKey )
					{
						// ... make sure the first target pixel won´t change.
						dwSourceTemp = (dwSourceTemp&0x0000FFFF) | (dwTargetTemp & 0xffff0000);
					}

					// If the second source is black ...
					if ( ( dwSourceTemp & 0xffff ) == colorKey )
					{
						// ... make sure the second target pixel won´t change.
						dwSourceTemp = (dwTargetTemp&0x0000FFFF) | (dwSourceTemp & 0xffff0000);
					}

					// Calculate the destination pixels.
					dwTargetTemp = ( ( dwTargetTemp & 0xf7def7de ) >> 1 ) + 
						( ( dwSourceTemp & 0xf7def7de ) >> 1 );

					// Write the destination pixels.
					*( ( DWORD* ) lpbTarget ) = dwTargetTemp;
				}

				//
				// Proceed to the next two pixels.
				//
				lpbTarget += 4;
				lpbSource += 4;
			}

			//
			// Handle an odd width.
			//
			if ( gOddWidth )
			{
				// Read in one source pixel.
				dwSourceTemp = *( ( WORD* ) lpbSource );

				// If this is not the color key ...
				if ( dwSourceTemp != colorKey )
				{
					//
					// ... apply the alpha blend to it.
					//

					// Read in one target pixel.
					dwTargetTemp = *( ( WORD* ) lpbTarget );

					// Write the destination pixel.
					*( ( WORD* ) lpbTarget ) = ( WORD ) 
						( ( ( dwTargetTemp & 0xf7de ) >> 1 ) + 
						  ( ( dwSourceTemp & 0xf7de ) >> 1 ) );
				}

				// 
				// Proceed to next pixel.
				//
				lpbTarget += 2;
				lpbSource += 2;
			}

			//
			// Proceed to the next line.
			//
			lpbTarget += dwTargetPad;
			lpbSource += dwSourcePad;
		} 
		while ( --iHeight > 0 );

		break;

	/* 16 bit mode ( unknown ). This algorithm 
	   can process two pixels at once. */
	case RGBMODE_16:
		ddsdTarget.lpSurface = static_cast<BYTE*>(ddsdTarget.lpSurface) + ddsdTarget.lPitch*iDestY + iDestX*2;
		ddsdSource.lpSurface = static_cast<BYTE*>(ddsdSource.lpSurface) + ddsdSource.lPitch*sourceY1 + sourceX1*2;
		//
		// Determine the padding bytes for the target and the source.
		//
		dwTargetPad = ddsdTarget.lPitch - ( iWidth * 2 );
		dwSourcePad = ddsdSource.lPitch - ( iWidth * 2 );

		// If the width is odd ...
		if ( iWidth & 0x01 )
		{
			// ... set the flag ...
			gOddWidth = true;

			// ... and calculate the width.
			iWidth = ( iWidth - 1 ) / 2;
		}
		// If the width is even ...
		else
		{
			// ... clear the flag ...
			gOddWidth = false;

			// ... and calculate the width.
			iWidth /= 2;
		}

		// Create the bit mask used to clear the lowest bit of each color channel´s mask.
		wMask = ( WORD ) ( ( ddsdTarget.ddpfPixelFormat.dwRBitMask & 
						   ( ddsdTarget.ddpfPixelFormat.dwRBitMask << 1 ) ) | 
						   ( ddsdTarget.ddpfPixelFormat.dwGBitMask & 
						   ( ddsdTarget.ddpfPixelFormat.dwGBitMask << 1 ) ) | 
						   ( ddsdTarget.ddpfPixelFormat.dwBBitMask & 
						   ( ddsdTarget.ddpfPixelFormat.dwBBitMask << 1 ) ) );

		// Create a double bit mask.
		dwDoubleMask = wMask | ( wMask << 16 );

		// Get the address of the target.
		lpbTarget = ( BYTE* ) ddsdTarget.lpSurface;

		// Get the address of the source.
		lpbSource = ( BYTE* ) ddsdSource.lpSurface;

		do
		{
			// Reset the width.
			i = iWidth;

			// 
			// Alpha-blend two pixels at once.
			//
			while ( i-- > 0 )
			{
				// Read in two source pixels.
				dwSourceTemp = *( ( DWORD* ) lpbSource );

				// If the two source pixels are not both black ...
				if ( dwSourceTemp != colorKey )
				{
					// ... read in two target pixels.
					dwTargetTemp = *( ( DWORD* ) lpbTarget );

					// If the first source is black ...
					if ( ( dwSourceTemp >> 16 ) == colorKey )
					{
						// ... make sure the first target pixel won´t change.
						dwSourceTemp |= dwTargetTemp & 0xffff0000;
					}

					// If the second source is black ...
					if ( ( dwSourceTemp & 0xffff ) == colorKey )
					{
						// ... make sure the second target pixel won´t change.
						dwSourceTemp |= dwTargetTemp & 0xffff;
					}

					// Calculate the destination pixels.
					dwTargetTemp = ( ( dwTargetTemp & dwDoubleMask ) >> 1 ) + 
						( ( dwSourceTemp & dwDoubleMask ) >> 1 );

					// Write the destination pixels.
					*( ( DWORD* ) lpbTarget ) = dwTargetTemp;
				}

				//
				// Proceed to the next two pixels.
				//
				lpbTarget += 4;
				lpbSource += 4;
			}

			//
			// Handle an odd width.
			//
			if ( gOddWidth )
			{
				// Read in one source pixel.
				dwSourceTemp = *( ( WORD* ) lpbSource );

				// If this is not the color key ...
				if ( dwSourceTemp != colorKey )
				{
					//
					// ... apply the alpha blend to it.
					//

					// Read in one target pixel.
					dwTargetTemp = *( ( WORD* ) lpbTarget );

					// Write the destination pixel.
					*( ( WORD* ) lpbTarget ) = ( WORD ) 
						( ( ( dwTargetTemp & wMask ) >> 1 ) + 
						  ( ( dwSourceTemp & wMask ) >> 1 ) );
				}

				// 
				// Proceed to next pixel.
				//
				lpbTarget += 2;
				lpbSource += 2;
			}

			//
			// Proceed to the next line.
			//
			lpbTarget += dwTargetPad;
			lpbSource += dwSourcePad;
		} 
		while ( --iHeight > 0 );

		break;

	/* 24 bit mode. */
	case RGBMODE_24:
		ddsdTarget.lpSurface = static_cast<BYTE*>(ddsdTarget.lpSurface) + ddsdTarget.lPitch*iDestY + iDestX*3;
		ddsdSource.lpSurface = static_cast<BYTE*>(ddsdSource.lpSurface) + ddsdSource.lPitch*sourceY1 + sourceX1*3;
		
		//
		// Determine the padding bytes for the target and the source.
		//
		dwTargetPad = ddsdTarget.lPitch - ( iWidth * 3 );
		dwSourcePad = ddsdSource.lPitch - ( iWidth * 3 );

		// Get the address of the target.
		lpbTarget = ( BYTE* ) ddsdTarget.lpSurface;

		// Get the address of the source.
		lpbSource = ( BYTE* ) ddsdSource.lpSurface;

		do
		{
			// Reset the width.
			i = iWidth;

			// 
			// Alpha-blend the pixels in the current row.
			//
			while ( i-- > 0 )
			{
				// Read in the next source pixel.
				dwSourceTemp = *( ( DWORD* ) lpbSource );	

				// If the source pixel is not black ...
				if ( ( dwSourceTemp & 0x00ffffff ) != colorKey )
				{
					// ... read in the next target pixel.
					dwTargetTemp = *( ( DWORD* ) lpbTarget );

					// Calculate the destination pixel.
					dwTargetTemp = ( ( dwTargetTemp & 0xfefefe ) >> 1 ) + 
								   ( ( dwSourceTemp & 0xfefefe ) >> 1 ); 

					//
					// Write the destination pixel.
					//
					*( ( WORD* ) lpbTarget ) = ( WORD ) dwTargetTemp;
					lpbTarget += 2;
					*lpbTarget = ( BYTE ) ( dwTargetTemp >> 16 );
					lpbTarget++;
				}
				// If the source pixel is our color key ...
				else
				{
					// ... advance the target pointer.
					lpbTarget += 3;
				}

				// Proceed to the next source pixel.
				lpbSource += 3;
			}

			//
			// Proceed to the next line.
			//
			lpbTarget += dwTargetPad;
			lpbSource += dwSourcePad;
		}
		while  ( --iHeight > 0 );

		break;

	/* 32 bit mode. */
	case RGBMODE_32:
		ddsdTarget.lpSurface = static_cast<BYTE*>(ddsdTarget.lpSurface) + ddsdTarget.lPitch*iDestY + iDestX*4;
		ddsdSource.lpSurface = static_cast<BYTE*>(ddsdSource.lpSurface) + ddsdSource.lPitch*sourceY1 + sourceX1*4;

		//
		// Determine the padding bytes for the target and the source.
		//
		dwTargetPad = ddsdTarget.lPitch - ( iWidth * 4 );
		dwSourcePad = ddsdSource.lPitch - ( iWidth * 4 );

		// Get the address of the target.
		lpbTarget = ( BYTE* ) ddsdTarget.lpSurface;

		// Get the address of the source.
		lpbSource = ( BYTE* ) ddsdSource.lpSurface;

		do
		{
			// Reset the width.
			i = iWidth;

			// 
			// Alpha-blend the pixels in the current row.
			//
			while ( i-- > 0 )
			{
				// Read in the next source pixel.
				dwSourceTemp = *( ( DWORD* ) lpbSource );	

				// If the source pixel is not black ...
                                if ( ( dwSourceTemp & 0xffffff ) != colorKey )
                                {
					// ... read in the next target pixel.
					dwTargetTemp = *( ( DWORD* ) lpbTarget );

					// Calculate the destination pixel.
					dwTargetTemp = ( ( dwTargetTemp & 0xfefefe ) >> 1 ) + 
								   ( ( dwSourceTemp & 0xfefefe ) >> 1 ); 

					// Write the destination pixel.
					*( ( DWORD* ) lpbTarget ) = dwTargetTemp;
				}

				//
				// Proceed to the next pixel.
				//
				lpbTarget += 4;
				lpbSource += 4;
			}

			//
			// Proceed to the next line.
			//
			lpbTarget += dwTargetPad;
			lpbSource += dwSourcePad;
		}
		while  ( --iHeight > 0 );

		break;

	/* Invalid mode. */
	default:
		iRet = -1;
	}

	// Unlock the target surface.
	lpDDSDest->Unlock( NULL );

	// Unlock the source surface.
	lpDDSSource->Unlock( NULL );

	// Return the result.
	return iRet==0?S_OK:E_FAIL;
}

HRESULT bltShape(
	IDirectDrawSurface7* lpDDSDest,
	IDirectDrawSurface7* lpDDSSource,
	int iDestX,
	int iDestY,
	int sourceX1, int sourceY1, int sourceX2, int sourceY2,
	int fillColor,
	DWORD colorKey ) {

	DDSURFACEDESC2	ddsdSource;
	DDSURFACEDESC2	ddsdTarget;
	DWORD			dwTargetPad;
	DWORD			dwSourcePad;
	DWORD			dwTargetTemp;
	DWORD			dwSourceTemp;
	WORD			wMask;
	DWORD			dwDoubleMask;
	BYTE*			lpbTarget;
	BYTE*			lpbSource;
	int				iWidth;
	int				iHeight;
	bool			gOddWidth;
	int				iRet = 0;
	int				i;


	if( dwMode==RGBMODE_UNCHECKED )
		init(lpDDSDest);

	//
	// Determine the dimensions of the source surface.
	//
//	if ( lprcSource )
	{
		//
		// Get the width and height from the passed rectangle.
		//
		iWidth =  sourceX2 -  sourceX1;
		iHeight = sourceY2 - sourceY1; 
	}
/*	else
	{
		//
		// Get the with and height from the surface description.
		//
///		memset( &ddsdSource, 0, sizeof ddsdSource );
///		ddsdSource.lSize = sizeof ddsdSource;
///		ddsdSource.dwFlags = DDSD_WIDTH | DDSD_HEIGHT;
/// ? couldn't figure out how to set this flag
		lpDDSSource->GetSurfaceDesc( &ddsdSource );

		//
		// Remember the dimensions.
		//
		iWidth = ddsdSource.lWidth;
		iHeight = ddsdSource.lHeight;
	}
*/


	//
	// Lock down the destination surface.
	//
	memset( &ddsdTarget, 0, sizeof ddsdTarget );
	ddsdTarget.dwSize = sizeof ddsdTarget;
	lpDDSDest->Lock( NULL, &ddsdTarget, DDLOCK_WAIT, NULL );  

	//
	// Lock down the source surface.
	//
	memset( &ddsdSource, 0, sizeof ddsdSource );
	ddsdSource.dwSize = sizeof ddsdSource;
	lpDDSSource->Lock( NULL, &ddsdSource, DDLOCK_WAIT, NULL );


/// Now this might be my problem, but ddsdTarget.lpSurface
/// doesn't seem to correctly reflect the lock region.
/// so I modified the code to lock the entire region and adjust lpSurface afterward.

	
	// clipping
	if(iDestX<0) {
		sourceX1 -= iDestX;
		iWidth += iDestX;
		iDestX=0;
	}
	if(iDestY<0) {
		sourceY1 -= iDestY;
		iHeight += iDestY;
		iDestY=0;
	}
	int extra;
	extra = (iDestX+iWidth)-ddsdTarget.dwWidth;
	if(extra>0) {
		sourceX2 -= extra;
		iWidth -= extra;
	}
	extra = (iDestY+iHeight)-ddsdTarget.dwHeight;
	if(extra>0) {
		sourceY2 -= extra;
		iHeight -= extra;
	}
	if( iWidth<=0 || iHeight<=0 ) {
		lpDDSDest->Unlock( NULL );
		lpDDSSource->Unlock( NULL );
		return true;	// no region to draw
	}

	//
	// Perform the blit operation.
	//
	switch ( dwMode )
	{
	case RGBMODE_555:
	case RGBMODE_565:
	case RGBMODE_16:
		ddsdTarget.lpSurface = static_cast<BYTE*>(ddsdTarget.lpSurface) + ddsdTarget.lPitch*iDestY + iDestX*2;
		ddsdSource.lpSurface = static_cast<BYTE*>(ddsdSource.lpSurface) + ddsdSource.lPitch*sourceY1 + sourceX1*2;
		//
		// Determine the padding bytes for the target and the source.
		//
		dwTargetPad = ddsdTarget.lPitch - ( iWidth * 2 );
		dwSourcePad = ddsdSource.lPitch - ( iWidth * 2 );

		// If the width is odd ...
		if ( iWidth & 0x01 )
		{
			// ... set the flag ...
			gOddWidth = true;

			// ... and calculate the width.
			iWidth = ( iWidth - 1 ) / 2;
		}
		// If the width is even ...
		else
		{
			// ... clear the flag ...
			gOddWidth = false;

			// ... and calculate the width.
			iWidth /= 2;
		}

		// Create the bit mask used to clear the lowest bit of each color channel´s mask.
		wMask = ( WORD ) ( ( ddsdTarget.ddpfPixelFormat.dwRBitMask & 
						   ( ddsdTarget.ddpfPixelFormat.dwRBitMask << 1 ) ) | 
						   ( ddsdTarget.ddpfPixelFormat.dwGBitMask & 
						   ( ddsdTarget.ddpfPixelFormat.dwGBitMask << 1 ) ) | 
						   ( ddsdTarget.ddpfPixelFormat.dwBBitMask & 
						   ( ddsdTarget.ddpfPixelFormat.dwBBitMask << 1 ) ) );

		// Create a double bit mask.
		dwDoubleMask = wMask | ( wMask << 16 );

		// Get the address of the target.
		lpbTarget = ( BYTE* ) ddsdTarget.lpSurface;

		// Get the address of the source.
		lpbSource = ( BYTE* ) ddsdSource.lpSurface;

		do
		{
			// Reset the width.
			i = iWidth;

			// 
			// Alpha-blend two pixels at once.
			//
			while ( i-- > 0 )
			{
				// Read in two source pixels.
				dwSourceTemp = *( ( DWORD* ) lpbSource );

				// If the two source pixels are not both black ...
				if ( dwSourceTemp != colorKey )
				{
					// ... read in two target pixels.
					dwTargetTemp = *( ( DWORD* ) lpbTarget );

					// If the first source is not black ...
					if ( ( dwSourceTemp >> 16 ) != colorKey )
					{
						dwTargetTemp = (fillColor<<16) | (dwTargetTemp&0x0000FFFF);
					}

					// If the second source is not black ...
					if ( ( dwSourceTemp & 0xffff ) != colorKey )
					{
						// ... make sure the second target pixel won´t change.
						dwTargetTemp = (dwTargetTemp & 0xFFFF0000)|fillColor;
					}

					// Write the destination pixels.
					*( ( DWORD* ) lpbTarget ) = dwTargetTemp;
				}

				//
				// Proceed to the next two pixels.
				//
				lpbTarget += 4;
				lpbSource += 4;
			}

			//
			// Handle an odd width.
			//
			if ( gOddWidth )
			{
				// Read in one source pixel.
				dwSourceTemp = *( ( WORD* ) lpbSource );

				// If this is not the color key ...
				if ( dwSourceTemp != colorKey )
				{
					*( ( WORD* ) lpbTarget ) = fillColor;
				}

				// 
				// Proceed to next pixel.
				//
				lpbTarget += 2;
				lpbSource += 2;
			}

			//
			// Proceed to the next line.
			//
			lpbTarget += dwTargetPad;
			lpbSource += dwSourcePad;
		} 
		while ( --iHeight > 0 );

		break;

	/* 24 bit mode. */
	case RGBMODE_24:
		ddsdTarget.lpSurface = static_cast<BYTE*>(ddsdTarget.lpSurface) + ddsdTarget.lPitch*iDestY + iDestX*3;
		ddsdSource.lpSurface = static_cast<BYTE*>(ddsdSource.lpSurface) + ddsdSource.lPitch*sourceY1 + sourceX1*3;
		
		//
		// Determine the padding bytes for the target and the source.
		//
		dwTargetPad = ddsdTarget.lPitch - ( iWidth * 3 );
		dwSourcePad = ddsdSource.lPitch - ( iWidth * 3 );

		// Get the address of the target.
		lpbTarget = ( BYTE* ) ddsdTarget.lpSurface;

		// Get the address of the source.
		lpbSource = ( BYTE* ) ddsdSource.lpSurface;

		do
		{
			// Reset the width.
			i = iWidth;

			// 
			// Alpha-blend the pixels in the current row.
			//
			while ( i-- > 0 )
			{
				// Read in the next source pixel.
				dwSourceTemp = *( ( DWORD* ) lpbSource );	

				// If the source pixel is not black ...
				if ( ( dwSourceTemp & 0x00ffffff ) != colorKey )
				{
					dwTargetTemp = fillColor;

					//
					// Write the destination pixel.
					//
					*( ( WORD* ) lpbTarget ) = ( WORD ) dwTargetTemp;
					lpbTarget += 2;
					*lpbTarget = ( BYTE ) ( dwTargetTemp >> 16 );
					lpbTarget++;
				}
				// If the source pixel is our color key ...
				else
				{
					// ... advance the target pointer.
					lpbTarget += 3;
				}

				// Proceed to the next source pixel.
				lpbSource += 3;
			}

			//
			// Proceed to the next line.
			//
			lpbTarget += dwTargetPad;
			lpbSource += dwSourcePad;
		}
		while  ( --iHeight > 0 );

		break;

	/* 32 bit mode. */
	case RGBMODE_32:
		ddsdTarget.lpSurface = static_cast<BYTE*>(ddsdTarget.lpSurface) + ddsdTarget.lPitch*iDestY + iDestX*4;
		ddsdSource.lpSurface = static_cast<BYTE*>(ddsdSource.lpSurface) + ddsdSource.lPitch*sourceY1 + sourceX1*4;

		//
		// Determine the padding bytes for the target and the source.
		//
		dwTargetPad = ddsdTarget.lPitch - ( iWidth * 4 );
		dwSourcePad = ddsdSource.lPitch - ( iWidth * 4 );

		// Get the address of the target.
		lpbTarget = ( BYTE* ) ddsdTarget.lpSurface;

		// Get the address of the source.
		lpbSource = ( BYTE* ) ddsdSource.lpSurface;

		do
		{
			// Reset the width.
			i = iWidth;

			// 
			// Alpha-blend the pixels in the current row.
			//
			while ( i-- > 0 )
			{
				// Read in the next source pixel.
				dwSourceTemp = *( ( DWORD* ) lpbSource );	

				// If the source pixel is not black ...
				if ( ( dwSourceTemp & 0xffffff ) != colorKey )
				{
					// Write the destination pixel.
					*( ( DWORD* ) lpbTarget ) = fillColor;
				}

				//
				// Proceed to the next pixel.
				//
				lpbTarget += 4;
				lpbSource += 4;
			}

			//
			// Proceed to the next line.
			//
			lpbTarget += dwTargetPad;
			lpbSource += dwSourcePad;
		}
		while  ( --iHeight > 0 );

		break;

	/* Invalid mode. */
	default:
		iRet = -1;
	}

	// Unlock the target surface.
	lpDDSDest->Unlock( NULL );

	// Unlock the source surface.
	lpDDSSource->Unlock( NULL );

	// Return the result.
	return iRet==0?S_OK:E_FAIL;
}

HRESULT bltColorTransform(
	IDirectDrawSurface7* lpDDSDest,
	IDirectDrawSurface7* lpDDSSource,
	int iDestX,
	int iDestY,
	int sourceX1, int sourceY1, int sourceX2, int sourceY2,
	int* srcColors,
	int* dstColors,
	int colorsLen,
	//IntArray* srcColors,
	//IntArray* dstColors,
	DWORD colorKey,
	BOOL vflip ) {

	DDSURFACEDESC2	ddsdSource;
	DDSURFACEDESC2	ddsdTarget;
	int			dwTargetPad;
	int			dwSourcePad;
	DWORD			dwTargetTemp;
	DWORD			dwSourceTemp;
	BYTE*			lpbTarget;
	BYTE*			lpbSource;
	int				iWidth;
	int				iHeight;
	bool			gOddWidth;
	int				iRet = 0;
	int				i;

	if( dwMode==RGBMODE_UNCHECKED )
		init(lpDDSDest);

	DWORD doubleColorKey = (colorKey<<16)+colorKey;
	
	//
	// Get the width and height from the passed rectangle.
	//
	iWidth =  sourceX2 - sourceX1;
	iHeight = sourceY2 - sourceY1; 

	int originalBottom = sourceY2;


	//
	// Lock down the destination surface.
	//
	memset( &ddsdTarget, 0, sizeof ddsdTarget );
	ddsdTarget.dwSize = sizeof ddsdTarget;
	lpDDSDest->Lock( NULL, &ddsdTarget, DDLOCK_WAIT, NULL );  

	//
	// Lock down the source surface.
	//
	memset( &ddsdSource, 0, sizeof ddsdSource );
	ddsdSource.dwSize = sizeof ddsdSource;
	lpDDSSource->Lock( NULL, &ddsdSource, DDLOCK_WAIT, NULL );


/// Now this might be my problem, but ddsdTarget.lpSurface
/// doesn't seem to correctly reflect the lock region.
/// so I modified the code to lock the entire region and adjust lpSurface afterward.

	
	// clipping
	if(iDestX<0) {
		sourceX1 -= iDestX;
		iWidth += iDestX;
		iDestX=0;
	}
	if(iDestY<0) {
		sourceY1 -= iDestY;
		iHeight += iDestY;
		iDestY=0;
	}
	int extra;
	extra = (iDestX+iWidth)-ddsdTarget.dwWidth;
	if(extra>0) {
		sourceX2 -= extra;
		iWidth -= extra;
	}
	extra = (iDestY+iHeight)-ddsdTarget.dwHeight;
	if(extra>0) {
		sourceY2 -= extra;
		iHeight -= extra;
	}
	if( iWidth<=0 || iHeight<=0 ) {
		lpDDSDest->Unlock( NULL );
		lpDDSSource->Unlock( NULL );
		return true;	// no region to draw
	}
	
	WORD mask16 = 0xFFFF;	// used to mask color in 16bit mode

	//
	// Perform the blit operation.
	//

    switch ( dwMode )
	{
	case RGBMODE_555:
		mask16 = 0x7FFF;	// fall through
	case RGBMODE_565:
	case RGBMODE_16:

		ddsdTarget.lpSurface = static_cast<PBYTE>(ddsdTarget.lpSurface) + ddsdTarget.lPitch*iDestY + iDestX*2;
		dwTargetPad = ddsdTarget.lPitch - ( iWidth * 2 );

		if( vflip ) {
			ddsdSource.lpSurface = static_cast<PBYTE>(ddsdSource.lpSurface) + ddsdSource.lPitch*(originalBottom-1) + sourceX1*2;
			dwSourcePad = - ddsdSource.lPitch - ( iWidth * 2 );
		} else {
			ddsdSource.lpSurface = static_cast<PBYTE>(ddsdSource.lpSurface) + ddsdSource.lPitch*sourceY1 + sourceX1*2;
			dwSourcePad = ddsdSource.lPitch - ( iWidth * 2 );
		}

		// If the width is odd ...
		if ( iWidth & 0x01 )
		{
			// ... set the flag ...
			gOddWidth = true;

			// ... and calculate the width.
			iWidth = ( iWidth - 1 ) / 2;
		}
		// If the width is even ...
		else
		{
			// ... clear the flag ...
			gOddWidth = false;

			// ... and calculate the width.
			iWidth /= 2;
		}

		// Get the address of the target.
		lpbTarget = ( BYTE* ) ddsdTarget.lpSurface;

		// Get the address of the source.
		lpbSource = ( BYTE* ) ddsdSource.lpSurface;

		do
		{
			// Reset the width.
			i = iWidth;

			// 
			// process two pixels at once.
			//
			while ( i-- > 0 )
			{
				// Read in two source pixels.
				dwSourceTemp = *( ( DWORD* ) lpbSource );

				// If the two source pixels are not both black ...
				if ( dwSourceTemp != doubleColorKey )
				{
					// ... read in two target pixels.
					dwTargetTemp = *( ( DWORD* ) lpbTarget );

					// If the first source is not the key color
					DWORD firstWord = dwSourceTemp>>16;
					if ( firstWord != colorKey ) {
						dwTargetTemp &= 0x0000FFFF;
						dwTargetTemp |= translateColor(firstWord&mask16,srcColors,dstColors,colorsLen) << 16;
					}

					// If the second source is not the key color
					DWORD secondWord = dwSourceTemp & 0x0000FFFF;
					if ( secondWord != colorKey ) {
						dwTargetTemp &= 0xFFFF0000;
						dwTargetTemp |= translateColor(secondWord&mask16,srcColors,dstColors,colorsLen);
					}

					// Write the destination pixels.
					*( ( DWORD* ) lpbTarget ) = dwTargetTemp;
				}

				//
				// Proceed to the next two pixels.
				//
				lpbTarget += 4;
				lpbSource += 4;
			}

			//
			// Handle an odd width.
			//
			if ( gOddWidth )
			{
				// Read in one source pixel.
				dwSourceTemp = *( ( WORD* ) lpbSource );

				// If this is not the color key ...
				if ( dwSourceTemp != colorKey )
					*( (WORD*) lpbTarget ) = (WORD)translateColor(dwSourceTemp&mask16,srcColors,dstColors,colorsLen);

				// 
				// Proceed to next pixel.
				//
				lpbTarget += 2;
				lpbSource += 2;
			}

			//
			// Proceed to the next line.
			//
			lpbTarget += dwTargetPad;
			lpbSource += dwSourcePad;
		} 
		while ( --iHeight > 0 );

		break;

	/* 24 bit mode. */
	case RGBMODE_24:
		//
		// Determine the padding bytes for the target and the source.
		//
		ddsdTarget.lpSurface = static_cast<PBYTE>(ddsdTarget.lpSurface) + ddsdTarget.lPitch*iDestY + iDestX*3;
		dwTargetPad = ddsdTarget.lPitch - ( iWidth * 3 );

		if(vflip) {
			ddsdSource.lpSurface = static_cast<PBYTE>(ddsdSource.lpSurface) + ddsdSource.lPitch*(originalBottom-1) + sourceX1*3;
			dwSourcePad = - ddsdSource.lPitch - ( iWidth * 3 );
		} else {
			ddsdSource.lpSurface = static_cast<PBYTE>(ddsdSource.lpSurface) + ddsdSource.lPitch*sourceY1 + sourceX1*3;
			dwSourcePad = ddsdSource.lPitch - ( iWidth * 3 );
		}

		// Get the address of the target.
		lpbTarget = ( BYTE* ) ddsdTarget.lpSurface;

		// Get the address of the source.
		lpbSource = ( BYTE* ) ddsdSource.lpSurface;

		do
		{
			// Reset the width.
			i = iWidth;

			// 
			// Alpha-blend the pixels in the current row.
			//
			while ( i-- > 0 )
			{
				// Read in the next source pixel.
				dwSourceTemp = *( ( DWORD* ) lpbSource );	
				dwSourceTemp &= 0x00FFFFFF;

				// If the source pixel is not black ...
				if ( dwSourceTemp != colorKey )
				{
					dwTargetTemp = translateColor(dwSourceTemp,srcColors,dstColors,colorsLen);

					//
					// Write the destination pixel.
					//
					*( ( WORD* ) lpbTarget ) = ( WORD ) dwTargetTemp;
					lpbTarget += 2;
					*lpbTarget = ( BYTE ) ( dwTargetTemp >> 16 );
					lpbTarget++;
				}
				// If the source pixel is our color key ...
				else
				{
					// ... advance the target pointer.
					lpbTarget += 3;
				}

				// Proceed to the next source pixel.
				lpbSource += 3;
			}

			//
			// Proceed to the next line.
			//
			lpbTarget += dwTargetPad;
			lpbSource += dwSourcePad;
		}
		while  ( --iHeight > 0 );

		break;

	/* 32 bit mode. */
	case RGBMODE_32:
		//
		// Determine the padding bytes for the target and the source.
		//
		ddsdTarget.lpSurface = static_cast<PBYTE>(ddsdTarget.lpSurface) + ddsdTarget.lPitch*iDestY + iDestX*4;
		dwTargetPad = ddsdTarget.lPitch - ( iWidth * 4 );

		if( vflip ) {
			ddsdSource.lpSurface = static_cast<PBYTE>(ddsdSource.lpSurface) + ddsdSource.lPitch*(originalBottom-1) + sourceX1*4;
			dwSourcePad = - ddsdSource.lPitch - ( iWidth * 4 );
		} else {
			ddsdSource.lpSurface = static_cast<PBYTE>(ddsdSource.lpSurface) + ddsdSource.lPitch*sourceY1 + sourceX1*4;
			dwSourcePad = ddsdSource.lPitch - ( iWidth * 4 );
		}

		// Get the address of the target.
		lpbTarget = ( BYTE* ) ddsdTarget.lpSurface;

		// Get the address of the source.
		lpbSource = ( BYTE* ) ddsdSource.lpSurface;

		do
		{
			// Reset the width.
			i = iWidth;

			// 
			// Alpha-blend the pixels in the current row.
			//
			while ( i-- > 0 )
			{
				// Read in the next source pixel.
				dwSourceTemp = *( ( DWORD* ) lpbSource );
				dwSourceTemp &= 0x00FFFFFF;

				// If the source pixel is not black ...
				if ( dwSourceTemp != colorKey )
					// Write the destination pixel.
					*( ( DWORD* ) lpbTarget ) = translateColor(dwSourceTemp,srcColors,dstColors,colorsLen);

				//
				// Proceed to the next pixel.
				//
				lpbTarget += 4;
				lpbSource += 4;
			}

			//
			// Proceed to the next line.
			//
			lpbTarget += dwTargetPad;
			lpbSource += dwSourcePad;
		}
		while  ( --iHeight > 0 );

		break;

	/* Invalid mode. */
	default:
		iRet = -1;
	}

	// Unlock the target surface.
	lpDDSDest->Unlock( NULL );

	// Unlock the source surface.
	lpDDSSource->Unlock( NULL );

	// Return the result.
	return iRet==0?S_OK:E_FAIL;
}

//------------------------------
// translate a color
#define RGB555(r,g,b)	(((r&0xf8)<<8)|((g&0xf8)<<2)|(b>>3))
#define RGB565(r,g,b)	(((r&0xf8)<<9)|((g&0xfc)<<2)|(b>>3))
#define	RGB888(r,g,b)	(((r   )<<16)|((g   )<<8)|(b   ))
#define RGB555to888(dw)	(((dw&0x1f)<<3)|((dw&0x3e0)<<6)|((dw&0x7c00)<<9))
#define RGB565to888(dw)	(((dw&0x1f)<<3)|((dw&0x7e0)<<5)|((dw&0xf800)<<8))
#define RGB888to555(dw)	(((dw&0xf8)>>3)|((dw&0xf800)>>6)|((dw&0xf80000)>>9))
#define RGB888to565(dw)	(((dw&0xf8)>>3)|((dw&0xfc00)>>5)|((dw&0xf80000)>>8))

static inline DWORD translateColor( DWORD color, int* srcColors, int* dstColors, int colorsLen ) {
	for( int i=colorsLen-1; i>=0; i-- )
		if( srcColors[i]==color )
			return (DWORD)dstColors[i];
	return color;
}

static inline DWORD rgb555( int r, int g, int b ) {
	return RGB555(r,g,b);
}

static inline DWORD rgb565( int r, int g, int b ) {
	return RGB565(r,g,b);
}

static inline DWORD rgb888( int r, int g, int b ) {
	return RGB888(r,g,b);
}

static inline DWORD rgb555to888( DWORD v ) {
	return RGB555to888(v);
}

static inline DWORD rgb565to888( DWORD v ) {
	return RGB565to888(v);
}

static inline DWORD rgb888to555( DWORD v ) {
	return RGB888to555(v);
}

static inline DWORD rgb888to565( DWORD v ) {
	return RGB888to565(v);
}

typedef DWORD (*MaskFunction)(int,int,int);

typedef DWORD (*ColorFmtConvertor)(DWORD);

struct MaskTest {
	DWORD mask;
	DWORD test;

	DWORD brightnessMask;
	int  bitShift;
	int   brightnessBits;

	inline MaskTest() {}
	inline MaskTest( DWORD _mask, DWORD _test, DWORD _b, int _s, int _bb ) :
		mask(_mask), test(_test), brightnessMask(_b), bitShift(_s), brightnessBits(_bb) {}

	// test a match to this mask
	template <typename T>
	inline bool match( T t ) {
		return (t&mask)==test;
	}

	template <typename T>
	inline T brightness( T t ) {
		return ((t&brightnessMask)<<(8-brightnessBits)) >>bitShift;
	}
};

struct HueTransformer {
	DWORD rgb[3];

	inline HueTransformer( DWORD Red_dst, DWORD Green_dst, DWORD Blue_dst ){
		rgb[0] = Red_dst; rgb[1] = Green_dst; rgb[2] = Blue_dst;
		for(int i=0; i<3; i++)
			if((rgb[i]&0xff000000)==0)
				rgb[i]=0;
	}

	// test a match to this mask
	template <typename T>
	inline T convert( T t ) {
		DWORD dest = t;
		if(t!=0xffffff&&t!=0){
			dest ^= procChannel(0,0xff0000,16,t);
			dest ^= procChannel(1,0x00ff00,8,t);
			dest ^= procChannel(2,0x0000ff,0,t);
		}
		return dest;
	}

	inline private DWORD procChannel(int c, DWORD mask, int shift, DWORD test){
		DWORD others = test & (~mask);
		if(rgb[c]==0 || others!=0) return 0;
		DWORD dst = rgb[c];
		DWORD bright = (test&mask)>>shift;
		DWORD r = dst&0xff0000;
		r = (r*bright+255)&0xff000000;
		DWORD g = dst&0x00ff00;
		g = (g*bright+255)&0xff0000;
		DWORD b = dst&0x0000ff;
		b = (b*bright+255)&0xff00;
		return test^(r|g|b)>>8;
	}
};

template < MaskFunction mask, int b1, int b2, int b3 >
inline MaskTest buildMask( int keyR, int keyG, int keyB ) {
	if( keyR==-1 )		return MaskTest( mask(0,255,255), mask(0,keyG,keyB), mask(255,0,0), b2+b3, b1 );
	if( keyG==-1 )		return MaskTest( mask(255,0,255), mask(keyR,0,keyB), mask(0,255,0),    b3, b2 );
						return MaskTest( mask(255,255,0), mask(keyR,keyG,0), mask(0,0,255),     0, b3 );
}

inline HueTransformer buildTransformer( DWORD R_dst, DWORD G_dst, DWORD B_dst ) {
	return HueTransformer( R_dst, G_dst, B_dst );
}

#define computeColor( word, R,G,B, rgbBuilder )	\
	(brightness=maskAndTest.brightness(word), \
	rgbBuilder( \
		(R*brightness)>>8, \
		(G*brightness)>>8, \
		(B*brightness)>>8))



template < ColorFmtConvertor encoder, ColorFmtConvertor decoder >
static inline void process16(
	int iDestX,
	int iDestY,
	DDSURFACEDESC2&	ddsdSource,
	DDSURFACEDESC2&	ddsdTarget,
	int sourceX1, int sourceY1, int /*sourceX2*/, int /*sourceY2*/,
	int R_dst, int G_dst, int B_dst,

	int iWidth,
	int iHeight,
	DWORD colorKey
	) {

	bool			gOddWidth;
	int				i;
//	DWORD			brightness;
	DWORD			dwTargetTemp;
	DWORD			dwSourceTemp;
	DWORD doubleColorKey = (colorKey<<16)|colorKey;

	//MaskTest maskAndTest = buildMask<rgbBuilder, R,G,B>(keyR,keyG,keyB);
	HueTransformer transformer = buildTransformer(R_dst,G_dst,B_dst);

	ddsdTarget.lpSurface = static_cast<BYTE*>(ddsdTarget.lpSurface) + ddsdTarget.lPitch*iDestY + iDestX*2;
	int dwTargetPad = ddsdTarget.lPitch - ( iWidth * 2 );
	
	ddsdSource.lpSurface = static_cast<BYTE*>(ddsdSource.lpSurface) + ddsdSource.lPitch*sourceY1 + sourceX1*2;
	int dwSourcePad = ddsdSource.lPitch - ( iWidth * 2 );
	
	// If the width is odd ...
	if ( iWidth & 0x01 )
	{
		// ... set the flag ...
		gOddWidth = true;

		// ... and calculate the width.
		iWidth = ( iWidth - 1 ) / 2;
	}
	// If the width is even ...
	else
	{
		// ... clear the flag ...
		gOddWidth = false;

		// ... and calculate the width.
		iWidth /= 2;
	}

	// Get the address of the target.
	BYTE* lpbTarget = ( BYTE* ) ddsdTarget.lpSurface;

	// Get the address of the source.
	BYTE* lpbSource = ( BYTE* ) ddsdSource.lpSurface;

	do
	{
		// Reset the width.
		i = iWidth;

		// 
		// process two pixels at once.
		//
		while ( i-- > 0 )
		{
			// Read in two source pixels.
			dwSourceTemp = *( ( DWORD* ) lpbSource );

			// If the two source pixels are not both black ...
			if ( dwSourceTemp != doubleColorKey )
			{
				// ... read in two target pixels.
				dwTargetTemp = *( ( DWORD* ) lpbTarget );

				// If the first source is not the key color
				DWORD firstWord = dwSourceTemp>>16;
				if ( firstWord != colorKey ) {
					dwTargetTemp &= 0x0000FFFF;
					dwTargetTemp |= encoder(transformer.convert(decoder(firstWord)))<<16;
				}

				// If the second source is not the key color
				DWORD secondWord = dwSourceTemp & 0x0000FFFF;
				if ( secondWord != colorKey ) {
					dwTargetTemp &= 0xFFFF0000;
					dwTargetTemp |=  encoder(transformer.convert(decoder(secondWord)));
				}

				// Write the destination pixels.
				*( ( DWORD* ) lpbTarget ) = dwTargetTemp;
			}

			//
			// Proceed to the next two pixels.
			//
			lpbTarget += 4;
			lpbSource += 4;
		}

		//
		// Handle an odd width.
		//
		if ( gOddWidth )
		{
			// Read in one source pixel.
			dwSourceTemp = *( ( WORD* ) lpbSource );

			// If this is not the color key ...
			if ( dwSourceTemp != colorKey ) {
				*( (WORD*)lpbTarget )
					= encoder(transformer.convert(decoder(dwSourceTemp)));
			}

			// 
			// Proceed to next pixel.
			//
			lpbTarget += 2;
			lpbSource += 2;
		}

		//
		// Proceed to the next line.
		//
		lpbTarget += dwTargetPad;
		lpbSource += dwSourcePad;
	} 
	while ( --iHeight > 0 );
}

HRESULT bltHueTransform(
	IDirectDrawSurface7* lpDDSDest,
	IDirectDrawSurface7* lpDDSSource,
	int iDestX,
	int iDestY,
	int sourceX1, int sourceY1, int sourceX2, int sourceY2,
	DWORD R_dst, DWORD G_dst, DWORD B_dst, DWORD colorKey ) {

	DDSURFACEDESC2	ddsdSource;
	DDSURFACEDESC2	ddsdTarget;
	int			dwTargetPad;
	int			dwSourcePad;
	DWORD			dwTargetTemp;
	DWORD			dwSourceTemp;
	BYTE*			lpbTarget;
	BYTE*			lpbSource;
	int				iWidth;
	int				iHeight;
	int				iRet = 0;
	int				i;
//	DWORD			brightness;

	MaskTest maskAndTest;

	if( dwMode==RGBMODE_UNCHECKED )
		init(lpDDSDest);

//	DWORD doubleColorKey = (colorKey<<16)+colorKey;
	
	memset( &ddsdSource, 0, sizeof ddsdSource ); // 2010.03.21 riorio fix
	ddsdSource.dwSize = sizeof ddsdSource; // 2010.03.21 riorio fix

	memset( &ddsdTarget, 0, sizeof ddsdTarget ); // 2010.03.21 riorio fix
	ddsdTarget.dwSize = sizeof ddsdTarget; // 2010.03.21 riorio fix
  
	//
	// Get the width and height from the passed rectangle.
	//
	iWidth =  sourceX2 -  sourceX1;
	iHeight = sourceY2 - sourceY1; 

//	int originalBottom = sourceY2;


	// Lock down the destination surface.
	lpDDSDest->Lock( NULL, &ddsdTarget, DDLOCK_WAIT, NULL );  

	// Lock down the source surface.
	lpDDSSource->Lock( NULL, &ddsdSource, DDLOCK_WAIT, NULL );


	// clipping
	if(iDestX<0) {
		sourceX1 -= iDestX;
		iWidth += iDestX;
		iDestX=0;
	}
	if(iDestY<0) {
		sourceY1 -= iDestY;
		iHeight += iDestY;
		iDestY=0;
	}
	int extra;
	extra = (iDestX+iWidth)-ddsdTarget.dwWidth;
	if(extra>0) {
		sourceX2 -= extra;
		iWidth -= extra;
	}
	extra = (iDestY+iHeight)-ddsdTarget.dwHeight;
	if(extra>0) {
		sourceY2 -= extra;
		iHeight -= extra;
	}
	if( iWidth<=0 || iHeight<=0 ) {
		lpDDSDest->Unlock( NULL );
		lpDDSSource->Unlock( NULL );
		return true;	// no region to draw
	}

	//
	// Perform the blit operation.
	//
	switch ( dwMode )
	{
	case RGBMODE_555:
		process16<rgb888to555, rgb555to888>(iDestX,iDestY,ddsdSource,ddsdTarget,
			sourceX1, sourceY1, sourceX2, sourceY2,
			R_dst, G_dst, B_dst,
			iWidth, iHeight, colorKey );
		break;

	case RGBMODE_565:
		process16<rgb888to565, rgb565to888>(iDestX,iDestY,ddsdSource,ddsdTarget,
			sourceX1, sourceY1, sourceX2, sourceY2,
			R_dst, G_dst, B_dst,
			iWidth, iHeight, colorKey );
		break;

	case RGBMODE_16:
		return E_NOTIMPL;



	/* 24 bit mode. */
	case RGBMODE_24:
		{
		//maskAndTest = buildMask<rgb888, 8,8,8>(keyR,keyG,keyB);
		HueTransformer transformer = buildTransformer( R_dst, G_dst, B_dst );
		//
		// Determine the padding bytes for the target and the source.
		//
		ddsdTarget.lpSurface = static_cast<BYTE*>(ddsdTarget.lpSurface) + ddsdTarget.lPitch*iDestY + iDestX*3;
		dwTargetPad = ddsdTarget.lPitch - ( iWidth * 3 );

		ddsdSource.lpSurface = static_cast<BYTE*>(ddsdSource.lpSurface) + ddsdSource.lPitch*sourceY1 + sourceX1*3;
		dwSourcePad = ddsdSource.lPitch - ( iWidth * 3 );

		// Get the address of the target.
		lpbTarget = ( BYTE* ) ddsdTarget.lpSurface;

		// Get the address of the source.
		lpbSource = ( BYTE* ) ddsdSource.lpSurface;

		do
		{
			// Reset the width.
			i = iWidth;

			// 
			// Alpha-blend the pixels in the current row.
			//
			while ( i-- > 0 )
			{
				// Read in the next source pixel.
				dwSourceTemp = *( ( DWORD* ) lpbSource ) & 0x00FFFFFF;
				if ( dwSourceTemp!=colorKey )
				{
					dwTargetTemp = transformer.convert(dwSourceTemp);

					//
					// Write the destination pixel.
					//
					*( ( WORD* ) lpbTarget ) = ( WORD ) dwTargetTemp;
					lpbTarget += 2;
					*lpbTarget = ( BYTE ) ( dwTargetTemp >> 16 );
					lpbTarget++;
				}
				else
					lpbTarget+=3;
				// Proceed to the next source pixel.
				lpbSource += 3;
			}

			//
			// Proceed to the next line.
			//
			lpbTarget += dwTargetPad;
			lpbSource += dwSourcePad;
		}
		while  ( --iHeight > 0 );

		break;
		}
	/* 32 bit mode. */
	case RGBMODE_32:
		{
		//maskAndTest = buildMask<rgb888, 8,8,8>(keyR,keyG,keyB);
		HueTransformer transformer = buildTransformer( R_dst, G_dst, B_dst );
		//
		// Determine the padding bytes for the target and the source.
		//
		ddsdTarget.lpSurface = static_cast<BYTE*>(ddsdTarget.lpSurface) + ddsdTarget.lPitch*iDestY + iDestX*4;
		dwTargetPad = ddsdTarget.lPitch - ( iWidth * 4 );

		ddsdSource.lpSurface = static_cast<BYTE*>(ddsdSource.lpSurface) + ddsdSource.lPitch*sourceY1 + sourceX1*4;
		dwSourcePad = ddsdSource.lPitch - ( iWidth * 4 );

		// Get the address of the target.
		lpbTarget = ( BYTE* ) ddsdTarget.lpSurface;

		// Get the address of the source.
		lpbSource = ( BYTE* ) ddsdSource.lpSurface;

		do
		{
			// Reset the width.
			i = iWidth;

			// 
			// Alpha-blend the pixels in the current row.
			//
			while ( i-- > 0 )
			{
				// Read in the next source pixel.
				dwSourceTemp = *( ( DWORD* ) lpbSource );
				dwSourceTemp &= 0x00FFFFFF;
				if ( dwSourceTemp!=colorKey )
					*( ( DWORD* ) lpbTarget ) = transformer.convert(dwSourceTemp);
				//
				// Proceed to the next pixel.
				//
				lpbTarget += 4;
				lpbSource += 4;
			}

			//
			// Proceed to the next line.
			//
			lpbTarget += dwTargetPad;
			lpbSource += dwSourcePad;
		}
		while  ( --iHeight > 0 );

		break;
		}
	/* Invalid mode. */
	default:
		iRet = -1;
	}

	// Unlock the target surface.
	lpDDSDest->Unlock( NULL );

	// Unlock the source surface.
	lpDDSSource->Unlock( NULL );

	// Return the result.
	return iRet==0?S_OK:E_FAIL;
}

//------------------------------
#pragma managed

System::String^ GetDisplayModeName() {
	switch( dwMode ) {
	case RGBMODE_555:		return "16bit (555)";
	case RGBMODE_565:		return "16bit (565)";
	case RGBMODE_16:		return "16bit (unknown)";
	case RGBMODE_24:		return "24bit";
	case RGBMODE_32:		return "32bit";
	case RGBMODE_UNCHECKED:	return "Unknown";
	default:				return "ERROR!";
	}
}

//--------------------

#pragma unmanaged

namespace
{
	template < WORD mask, WORD color1, WORD light1, WORD color2, WORD light2, WORD color3, WORD light3 >
	inline void darken16( int iWidth, int iHeight, DWORD lPitch, WORD* lpSurface ) {
		
		const DWORD pad = (lPitch - ( iWidth * 2 ))/2;

		for( ; iHeight>0; iHeight-- ) {
			for( int i=iWidth; i>0; i-- ) {
				// Read in a pixel
				WORD pix = *lpSurface;
				
				if( pix==color1 )	pix=light1;
				else
				if( pix==color2 )	pix=light2;
				else
				if( pix==color3 )	pix=light3;
				else
					pix = (pix&mask)>>2;		// pix /= 4

				*lpSurface = pix;

				lpSurface++;
			}

			// Proceed to the next line.
			lpSurface += pad;
		}
	}
}
HRESULT buildNightImage( IDirectDrawSurface7* lpSurface ) {

	DDSURFACEDESC2	ddsd;
	memset( &ddsd, 0, sizeof ddsd ); // 2010.03.21 riorio fix
	ddsd.dwSize = sizeof ddsd; // 2010.03.21 riorio fix

	if( dwMode==RGBMODE_UNCHECKED )
		init(lpSurface);

	lpSurface->Lock( NULL, &ddsd, DDLOCK_WAIT, NULL );  

	switch ( dwMode )
	{
	case RGBMODE_555:
		darken16<0x739C,
			RGB555(8,0,0),	RGB555(255,  8,  8),
			RGB555(0,8,0),	RGB555(252,243,148),
			RGB555(0,0,8),	RGB555(255,227, 99)
		>( ddsd.dwWidth, ddsd.dwHeight, ddsd.lPitch, (WORD*)(ddsd.lpSurface) );
		break;
	case RGBMODE_565:
		darken16<0xE79C,
			RGB565(8,0,0),	RGB565(255,  8,  8),
			RGB565(0,8,0),	RGB565(252,243,148),
			RGB565(0,0,8),	RGB565(255,227, 99)
		>( ddsd.dwWidth, ddsd.dwHeight, ddsd.lPitch, (WORD*)(ddsd.lpSurface) );
		break;
	case RGBMODE_16:
		// TODO
		return E_NOTIMPL;

	case RGBMODE_24:
	case RGBMODE_32:
		const int offset = (dwMode==RGBMODE_24)?3:4;
		const DWORD pad = ddsd.lPitch - ( ddsd.dwWidth * offset );
		LPBYTE lpbTarget = LPBYTE(ddsd.lpSurface);

		for( int iHeight=ddsd.dwHeight; iHeight>0; iHeight-- ) {
			for( int i=ddsd.dwWidth; i>0; i-- ) {
				// Read in a pixel
				DWORD pix = *reinterpret_cast<LPDWORD>(lpbTarget);
				DWORD lower24 = pix&0x00FFFFFF;

				if( lower24==RGB888(8,0,0) )	pix = (pix&0xFF000000)|RGB888(255,  8,  8);
				else
				if( lower24==RGB888(0,8,0) )	pix = (pix&0xFF000000)|RGB888(252,243,148);
				else
				if( lower24==RGB888(0,0,8) )	pix = (pix&0xFF000000)|RGB888(255,227, 99);
				else
												pix = (pix&0xFF000000)|((pix&0x00FCFCFC)>>2);// pix /= 4

				*reinterpret_cast<LPDWORD>(lpbTarget) = pix;

				lpbTarget+=offset;
			}

			// Proceed to the next line.
			lpbTarget += pad;
		}
		break;
	}

	lpSurface->Unlock( NULL );  

	return S_OK;
}
