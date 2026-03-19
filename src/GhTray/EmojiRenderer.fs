namespace GhTray

open System
open System.Drawing
open System.Drawing.Imaging
open System.Runtime.InteropServices
open Vortice.Direct2D1
open Vortice.DirectWrite
open Vortice.WIC
open Vortice.Mathematics

module EmojiRenderer =
    let private d2dFactory = D2D1.D2D1CreateFactory<ID2D1Factory>()
    let private dwFactory = DWrite.DWriteCreateFactory<IDWriteFactory>()
    let private wicFactory = new IWICImagingFactory2()

    let private cache = Collections.Generic.Dictionary<string, Bitmap>()

    let private renderToBitmap (emoji: string) (size: int) : Bitmap =
        let usize = uint32 size

        let wicBitmap =
            wicFactory.CreateBitmap(usize, usize, PixelFormat.Format32bppPBGRA, BitmapCreateCacheOption.CacheOnLoad)

        let rtProps =
            RenderTargetProperties(
                PixelFormat =
                    Vortice.DCommon.PixelFormat(
                        Vortice.DXGI.Format.B8G8R8A8_UNorm,
                        Vortice.DCommon.AlphaMode.Premultiplied
                    )
            )

        use rt = d2dFactory.CreateWicBitmapRenderTarget(wicBitmap, rtProps)

        let textFormat = dwFactory.CreateTextFormat("Segoe UI Emoji", float32 size * 0.75f)

        textFormat.TextAlignment <- TextAlignment.Center
        textFormat.ParagraphAlignment <- ParagraphAlignment.Center

        use brush = rt.CreateSolidColorBrush(Color4(1.0f, 1.0f, 1.0f, 1.0f))

        rt.BeginDraw()
        rt.Clear(Nullable<Color4>())

        rt.DrawText(
            emoji,
            textFormat,
            Rect(0.0f, 0.0f, float32 size, float32 size),
            brush,
            DrawTextOptions.EnableColorFont
        )

        rt.EndDraw() |> ignore

        let stride = usize * 4u
        let bufferSize = stride * usize
        let pixels = Array.zeroCreate<byte> (int bufferSize)

        let rect = RectI(0, 0, size, size)

        wicBitmap.CopyPixels(rect, stride, pixels)

        let bmp = new Bitmap(size, size, PixelFormat.Format32bppPArgb)

        let bmpData =
            bmp.LockBits(Rectangle(0, 0, size, size), ImageLockMode.WriteOnly, PixelFormat.Format32bppPArgb)

        Marshal.Copy(pixels, 0, bmpData.Scan0, int bufferSize)
        bmp.UnlockBits bmpData
        bmp

    let render (emoji: string) : Bitmap =
        match cache.TryGetValue emoji with
        | true, bmp -> bmp
        | false, _ ->
            let bmp = renderToBitmap emoji 20
            cache.[emoji] <- bmp
            bmp
