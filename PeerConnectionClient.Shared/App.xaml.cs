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
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;
using PeerConnectionClient.ViewModels;
using HockeyApp;

namespace PeerConnectionClient
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public sealed partial class App
    {
#if WINDOWS_PHONE_APP
        private TransitionCollection _transitions;
#endif
        private MainViewModel _mainViewModel;

        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            InitializeComponent();
            Suspending += OnSuspending;

            // configure hockey app SDK with correct app ID for current device
#if WINDOWS_PHONE_APP
            HockeyClient.Current.Configure("554a20152df3077b8ffca13f6eedc686");
#else
            HockeyClient.Current.Configure("e95ace8ed81020bd1b3c468f59a1f834");
#endif
        }

        /// <summary>
        /// Invoked when the application is launched normally by the end user.  Other entry points
        /// will be used when the application is launched to open a specific file, to display
        /// search results, and so forth.
        /// </summary>
        /// <param name="e">Details about the launch request and process.</param>
        protected async override void OnLaunched(LaunchActivatedEventArgs e)
        {
#if DEBUG
            //if (System.Diagnostics.Debugger.IsAttached)
            //{
            //    this.DebugSettings.EnableFrameRateCounter = true;
            //}
#endif

            var rootFrame = Window.Current.Content as Frame;

            // Do not repeat app initialization when the Window already has content,
            // just ensure that the window is active
            if (rootFrame == null)
            {
                // Create a Frame to act as the navigation context and navigate to the first page
                rootFrame = new Frame
                {
                    CacheSize = 1
                };

                // Place the frame in the current Window
                Window.Current.Content = rootFrame;
            }

            if (rootFrame.Content == null)
            {
#if WINDOWS_PHONE_APP
                // Removes the turnstile navigation for startup.
                if (rootFrame.ContentTransitions != null)
                {
                    _transitions = new TransitionCollection();
                    foreach (var c in rootFrame.ContentTransitions)
                    {
                        _transitions.Add(c);
                    }
                }

                rootFrame.ContentTransitions = null;
                rootFrame.Navigated += RootFrame_FirstNavigated;
#endif

                // When the navigation stack isn't restored navigate to the first page,
                // configuring the new page by passing required information as a navigation
                // parameter
                //if (!rootFrame.Navigate(typeof(MainPage), e.Arguments))
                if (!rootFrame.Navigate(typeof(ExtendedSplashScreen), e.SplashScreen))
                {
                    throw new Exception("Failed to create extended splashscreen");
                }
            }

            //Do not activate now, will be activated by ExtendedSplashScreen.
            //https://msdn.microsoft.com/en-us/library/windows/apps/hh465338.aspx:
            //"Flicker occurs if you activate the current window (by calling Window.Current.Activate)
            //before the content of the page finishes rendering. You can reduce the likelihood of seeing
            //a flicker by making sure your extended splash screen image has been read before you activate
            //the current window. Additionally, you should use a timer to try to avoid the flicker by
            //making your application wait briefly, 50ms for example, before you activate the current window.
            //Unfortunately, there is no guaranteed way to prevent the flicker because XAML renders content
            //asynchronously and there is no guaranteed way to predict when rendering will be complete."
            //Window.Current.Activate();
            _mainViewModel = new MainViewModel(CoreApplication.MainView.CoreWindow.Dispatcher);
            _mainViewModel.OnInitialized += OnMainViewModelInitialized;

            await HockeyClient.Current.SendCrashesAsync(true);

#if WINDOWS_PHONE_APP
            await HockeyClient.Current.CheckForAppUpdateAsync(); // updates only supported for WP apps
#endif
        }

#if WINDOWS_PHONE_APP
        /// <summary>
        /// Restores the content transitions after the app has launched.
        /// </summary>
        /// <param name="sender">The object where the handler is attached.</param>
        /// <param name="e">Details about the navigation event.</param>
        private void RootFrame_FirstNavigated(object sender, NavigationEventArgs e)
        {
            var rootFrame = (Frame) sender;
            rootFrame.ContentTransitions = _transitions ?? new TransitionCollection { new NavigationThemeTransition() };
            rootFrame.Navigated -= RootFrame_FirstNavigated;
        }
#endif

        protected override void OnActivated(IActivatedEventArgs e)
        {
            base.OnActivated(e);
#if WINDOWS_PHONE_APP
            HockeyClient.Current.HandleReactivationOfFeedbackFilePicker(e);
#endif
        }

        /// <summary>
        /// Invoked when application execution is being suspended.  Application state is saved
        /// without knowing whether the application will be terminated or resumed with the contents
        /// of memory still intact.
        /// </summary>
        /// <param name="sender">The source of the suspend request.</param>
        /// <param name="e">Details about the suspend request.</param>
        private void OnSuspending(object sender, SuspendingEventArgs e)
        {
            var deferral = e.SuspendingOperation.GetDeferral();
            // Perform suspending logic on non UI thread to avoid deadlocks,
            // since some ongoing flows may need access to UI thread
            new System.Threading.Tasks.Task(async () =>
            {
                await _mainViewModel.OnAppSuspending();
                deferral.Complete();
            }).Start();
        }

        /// <summary>
        /// Invoked when the application MainViewModel is initialized.
        /// Creates the application initial page
        /// </summary>
        private void OnMainViewModelInitialized()
        {
            var rootFrame = (Frame) Window.Current.Content;
            if (!rootFrame.Navigate(typeof(MainPage), _mainViewModel))
            {
                throw new Exception("Failed to create initial page");
            }
        }
    }
}