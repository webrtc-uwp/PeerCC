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

using Windows.UI.Xaml;

// The Settings Flyout item template is documented at http://go.microsoft.com/fwlink/?LinkId=273769

namespace PeerConnectionClient
{
    public sealed partial class ConnectionSettingsFlyout
    {
        public ConnectionSettingsFlyout()
        {
            InitializeComponent();
        }

        private void ConfirmAddButton_Click(object sender, RoutedEventArgs e)
        {
            AddButton.Flyout.Hide();
        }
    }
}
