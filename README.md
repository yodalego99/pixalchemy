# PixAlchemy

PixAlchemy is an Avalonia desktop app for video and image effects powered by OpenCV (Emgu CV).

The repository contains multiple platform folders, but the actively used app is the desktop build.

## Main Features

### 1. Background Removal (ViBe)

- Process input from:
	- Video file
	- Live webcam
- Algorithm: ViBe background subtraction
- Configurable options before processing:
	- `Enable shaky camera detection`
	- Segmentation output type:
		- `Segmentation mask`
		- `Background image`
		- `Foreground overlay`
- For file input, output can be exported as `.mp4` or `.avi`.

### 2. Mosaic Effect

- Applies a tile-based photo mosaic to video frames (or webcam feed preview).
- For file input, output can be exported as `.mp4` or `.avi`.

#### Mosaic tile folder

Place your custom tile images in:

`ImageProcessingFinal/MosaicTiles/`

Supported tile formats include:

- `.png`
- `.jpg` / `.jpeg`
- `.bmp`
- `.gif`
- `.tif` / `.tiff`

If no valid tiles are found, a built-in fallback color palette is used.

### 3. Particle Morph

- Morphs one image into another using moving particle blocks.
- Workflow:
	- Pick target image
	- Pick source image
	- Set particle size (`1` to `61` pixels)
	- Optionally save animation as video (`.mp4` or `.avi`)

## UI Overview

- Left panel: source/original preview
- Right panel: processed output preview
- Bottom: timeline slider + play/stop controls
- Menu:
	- `File` -> open video or webcam
	- `Edit` -> run processing features

## Requirements

- Windows (desktop target is configured for `win-x64`)
- .NET SDK 9.0+

## Run (Desktop)

From repository root:

```bash
dotnet run --project ImageProcessingFinal.Desktop/ImageProcessingFinal.Desktop.csproj
```

