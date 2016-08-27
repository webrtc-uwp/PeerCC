//*********************************************************
//
// Copyright (c) Microsoft. All rights reserved.
// This code is licensed under the MIT License (MIT).
// THIS CODE IS PROVIDED *AS IS* WITHOUT WARRANTY OF
// ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING ANY
// IMPLIED WARRANTIES OF FITNESS FOR A PARTICULAR
// PURPOSE, MERCHANTABILITY, OR NON-INFRINGEMENT.
//
//*********************************************************

using System;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace PeerConnectionClient
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class ExtendedSplashScreen : Page
    {
        private SplashScreen _splash; // Variable to hold the splash screen object.
        private DispatcherTimer _showWindowTimer;

        public ExtendedSplashScreen()
        {
            InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            _splash = (SplashScreen)e.Parameter;
            if (_splash != null)
            {
                PositionImage();
            }
        }

        void PositionImage()
        {
            var splashImageRect = _splash.ImageLocation;
            extendedSplashImage.SetValue(Canvas.LeftProperty, splashImageRect.X);
            extendedSplashImage.SetValue(Canvas.TopProperty, splashImageRect.Y);
            extendedSplashImage.Height = splashImageRect.Height;
            extendedSplashImage.Width = splashImageRect.Width;
        }

        void ExtendedSplash_OnResize(Object sender, WindowSizeChangedEventArgs e)
        {
            // Safely update the extended splash screen image coordinates. This function will be fired in response to snapping, unsnapping, rotation, etc...
            if (_splash != null)
            {
                // Update the coordinates of the splash screen image.
                PositionImage();
            }
        }

       

        //https://msdn.microsoft.com/en-us/library/windows/apps/hh465338.aspx:
        //"Flicker occurs if you activate the current window (by calling Window.Current.Activate)
        //before the content of the page finishes rendering. You can reduce the likelihood of seeing
        //a flicker by making sure your extended splash screen image has been read before you activate
        //the current window. Additionally, you should use a timer to try to avoid the flicker by
        //making your application wait briefly, 50ms for example, before you activate the current window.
        //Unfortunately, there is no guaranteed way to prevent the flicker because XAML renders content
        //asynchronously and there is no guaranteed way to predict when rendering will be complete."
        private void extendedSplashImage_ImageOpened(object sender, RoutedEventArgs e)
        {
            // ImageOpened means the file has been read, but the image hasn't been painted yet.
            // Start a short timer to give the image a chance to render, before showing the window
            // and starting the animation.
            _showWindowTimer = new DispatcherTimer {Interval = TimeSpan.FromMilliseconds(50)};
            _showWindowTimer.Tick += OnShowWindowTimer;
            _showWindowTimer.Start();
        }
        private void OnShowWindowTimer(object sender, object e)
        {
            _showWindowTimer.Stop();
            // Activate/show the window, now that the splash image has rendered
            Window.Current.Activate();
        }
    }
}
