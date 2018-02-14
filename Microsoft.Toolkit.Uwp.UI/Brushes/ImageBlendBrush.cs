﻿// ******************************************************************
// Copyright (c) Microsoft. All rights reserved.
// This code is licensed under the MIT License (MIT).
// THE CODE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
// INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.
// IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM,
// DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT,
// TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH
// THE CODE OR THE USE OR OTHER DEALINGS IN THE CODE.
// ******************************************************************

using Microsoft.Graphics.Canvas.Effects;
using System;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;

namespace Microsoft.Toolkit.Uwp.UI.Brushes
{
    /// <summary>
    /// Brush which blends an image to the Backdrop in a given mode. http://microsoft.github.io/Win2D/html/T_Microsoft_Graphics_Canvas_Effects_BlendEffect.htm
    /// Image loading reference from https://blogs.windows.com/buildingapps/2017/07/18/working-brushes-content-xaml-visual-layer-interop-part-one/#MA0k4EYWzqGKV501.97
    /// </summary>
    public class ImageBlendBrush : XamlCompositionBrushBase
    {
        private LoadedImageSurface _surface;
        private CompositionSurfaceBrush _surfaceBrush;

        /// <summary>
        /// Identifies the <see cref="Source"/> dependency property.
        /// </summary>
        public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(
            nameof(Source),
            typeof(string),
            typeof(ImageBlendBrush),
            new PropertyMetadata(null, new PropertyChangedCallback(OnImageSourceChanged)));

        /// <summary>
        /// Gets or sets the source of the image to composite.
        /// </summary>
        public string Source
        {
            get { return (string)GetValue(SourceProperty); }
            set { SetValue(SourceProperty, value); }
        }

        /// <summary>
        /// Identifies the <see cref="Stretch"/> dependency property.
        /// </summary>
        public static readonly DependencyProperty StretchProperty = DependencyProperty.Register(
            nameof(Stretch),
            typeof(Stretch),
            typeof(ImageBlendBrush),
            new PropertyMetadata(Stretch.Uniform, new PropertyChangedCallback(OnStretchChanged)));

        /// <summary>
        /// Gets or sets how to stretch the image within the brush.
        /// </summary>
        public Stretch Stretch
        {
            get { return (Stretch)GetValue(StretchProperty); }
            set { SetValue(StretchProperty, value); }
        }

        /// <summary>
        /// Identifies the <see cref="Mode"/> dependency property.
        /// </summary>
        public static readonly DependencyProperty ModeProperty = DependencyProperty.Register(
            nameof(Mode),
            typeof(ImageBlendMode),
            typeof(ImageBlendBrush),
            new PropertyMetadata(ImageBlendMode.Multiply, new PropertyChangedCallback(OnModeChanged)));

        /// <summary>
        /// Gets or sets how to blend the image with the backdrop.
        /// </summary>
        public ImageBlendMode Mode
        {
            get { return (ImageBlendMode)GetValue(ModeProperty); }
            set { SetValue(ModeProperty, value); }
        }

        private static void OnImageSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var brush = (ImageBlendBrush)d;

            // Unbox and update surface if CompositionBrush exists
            if (brush._surfaceBrush != null)
            {
                // TODO: Error Handling
                var newSurface = LoadedImageSurface.StartLoadFromUri(new Uri((string)e.NewValue));
                brush._surface = newSurface;
                brush._surfaceBrush.Surface = newSurface;
            }
        }

        private static void OnStretchChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var brush = (ImageBlendBrush)d;

            // Unbox and update surface if CompositionBrush exists
            if (brush._surfaceBrush != null)
            {
                // Modify the stretch property on our brush.
                brush._surfaceBrush.Stretch = CompositionStretchFromStretch((Stretch)e.NewValue);
            }
        }

        private static void OnModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var brush = (ImageBlendBrush)d;

            // We can't animate our enum properties so recreate our internal brush.
            brush.OnDisconnected();
            brush.OnConnected();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ImageBlendBrush"/> class.
        /// </summary>
        public ImageBlendBrush()
        {
            this.FallbackColor = Colors.Transparent;
        }

        /// <summary>
        /// Initializes the Composition Brush.
        /// </summary>
        protected override void OnConnected()
        {
            // Delay creating composition resources until they're required.
            if (CompositionBrush == null)
            {
                // TODO: Error Handle
                // Use LoadedImageSurface API to get ICompositionSurface from image uri provided
                _surface = LoadedImageSurface.StartLoadFromUri(new Uri(Source));

                // Load Surface onto SurfaceBrush
                _surfaceBrush = Window.Current.Compositor.CreateSurfaceBrush(_surface);
                _surfaceBrush.Stretch = CompositionStretchFromStretch(Stretch);

                // Abort if effects aren't supported.
                if (!CompositionCapabilities.GetForCurrentView().AreEffectsSupported())
                {
                    // Just use image straight-up, if we don't support effects.
                    CompositionBrush = _surfaceBrush;
                    return;
                }

                var backdrop = Window.Current.Compositor.CreateBackdropBrush();

                // Use a Win2D invert affect applied to a CompositionBackdropBrush.
                var graphicsEffect = new BlendEffect
                {
                    Name = "Invert",
                    Mode = (BlendEffectMode)(int)this.Mode,
                    Background = new CompositionEffectSourceParameter("backdrop"),
                    Foreground = new CompositionEffectSourceParameter("image")
                };

                var effectFactory = Window.Current.Compositor.CreateEffectFactory(graphicsEffect);
                var effectBrush = effectFactory.CreateBrush();

                effectBrush.SetSourceParameter("backdrop", backdrop);
                effectBrush.SetSourceParameter("image", _surfaceBrush);

                CompositionBrush = effectBrush;
            }
        }

        /// <summary>
        /// Deconstructs the Composition Brush.
        /// </summary>
        protected override void OnDisconnected()
        {
            // Dispose of composition resources when no longer in use.
            if (CompositionBrush != null)
            {
                CompositionBrush.Dispose();
                CompositionBrush = null;
            }

            if (_surfaceBrush != null)
            {
                _surfaceBrush?.Dispose();
                _surfaceBrush = null;
            }

            if (_surface != null)
            {
                _surface?.Dispose();
                _surface = null;
            }
        }

        //// Helper to allow XAML developer to use XAML stretch property rather than another enum.
        private static CompositionStretch CompositionStretchFromStretch(Stretch value)
        {
            switch (value)
            {
                case Stretch.None:
                    return CompositionStretch.None;
                case Stretch.Fill:
                    return CompositionStretch.Fill;
                case Stretch.Uniform:
                    return CompositionStretch.Uniform;
                case Stretch.UniformToFill:
                    return CompositionStretch.UniformToFill;
            }

            return CompositionStretch.None;
        }
    }
}
