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

using Windows.UI.ApplicationSettings;
using Windows.UI.Xaml.Navigation;
using PeerConnectionClient.SettingsFlyouts;
using PeerConnectionClient.ViewModels;

namespace PeerConnectionClient
{
    /// <summary>
    /// The application main page.
    /// </summary>
    public sealed partial class MainPage
    {
        private readonly DebugSettingsFlyout _debugSettingsFlyout;
        private readonly ConnectionSettingsFlyout _connectionSettingsFlyout;
        private readonly AudioVideoSettingsFlyout _audioVideoSettingsFlyout;
        private readonly AboutSettingsFlyout _aboutSettingsFlyout;

        private MainViewModel _mainViewModel;

        /// <summary>
        /// Constructor.
        /// </summary>
        public MainPage()
        {
            InitializeComponent();
            _debugSettingsFlyout = new DebugSettingsFlyout();
            _connectionSettingsFlyout = new ConnectionSettingsFlyout();
            _audioVideoSettingsFlyout = new AudioVideoSettingsFlyout();
            _aboutSettingsFlyout = new AboutSettingsFlyout();
            SettingsPane.GetForCurrentView().CommandsRequested += OnCommandsRequested;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            _mainViewModel = (MainViewModel)e.Parameter;
            DataContext = _debugSettingsFlyout.DataContext
              = _connectionSettingsFlyout.DataContext
              = _audioVideoSettingsFlyout.DataContext
              = _aboutSettingsFlyout.DataContext
              = _mainViewModel;
            _mainViewModel.PeerVideo = PeerVideo;
            _mainViewModel.SelfVideo = SelfVideo;
        }

        private void OnCommandsRequested(SettingsPane sender, SettingsPaneCommandsRequestedEventArgs args)
        {
            args.Request.ApplicationCommands.Add(new SettingsCommand(
                "ConnectionSettings", "Connection", handler => ShowConectionSettingsFlyout()));
            args.Request.ApplicationCommands.Add(new SettingsCommand(
                "AudioVideo", "Audio & Video", handler => ShowAudioVideoSettingsFlyout()));
            args.Request.ApplicationCommands.Add(new SettingsCommand(
                "DebugSettings", "Debug", handler => ShowDebugSettingFlyout()));
            args.Request.ApplicationCommands.Add(new SettingsCommand(
                "AboutSettings", "About", handler => ShowAboutSettingsFlyout()));
        }

        /// <summary>
        /// Shows the Debug settings layout
        /// </summary>
        public void ShowDebugSettingFlyout()
        {
            _debugSettingsFlyout.Show();
        }

        /// <summary>
        /// Shows the Connection settings layout
        /// </summary>
        public void ShowConectionSettingsFlyout()
        {
            _connectionSettingsFlyout.Show();
        }

        /// <summary>
        /// Shows the Audio and Video settings layout
        /// </summary>
        public void ShowAudioVideoSettingsFlyout()
        {
            _audioVideoSettingsFlyout.Show();
        }

        /// <summary>
        /// Shows the About setting layout
        /// </summary>
        public void ShowAboutSettingsFlyout()
        {
            _aboutSettingsFlyout.Show();
        }

        /// <summary>
        /// Media Failed event handler for remote/peer video.
        /// Invoked when an error occurs in peer media source.
        /// </summary>
        /// <param name="sender">The object where the handler is attached.</param>
        /// <param name="e">Details about the exception routed event.</param>
        private void PeerVideo_MediaFailed(object sender, Windows.UI.Xaml.ExceptionRoutedEventArgs e)
        {
            if(_mainViewModel!=null)
            {
                _mainViewModel.PeerVideo_MediaFailed(sender, e);
            }
        }

        /// <summary>
        /// Media Failed event handler for self video.
        /// Invoked when an error occurs in self media source.
        /// </summary>
        /// <param name="sender">The object where the handler is attached.</param>
        /// <param name="e">Details about the exception routed event.</param>
        private void SelfVideo_MediaFailed(object sender, Windows.UI.Xaml.ExceptionRoutedEventArgs e)
        {
            if (_mainViewModel != null)
            {
                _mainViewModel.SelfVideo_MediaFailed(sender, e);
            }
        }
    }
}
