﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

using ChatClassLibrary;
using ChatClassLibrary.Protocols;

namespace ChatClientWPF
{
    public partial class MainWindow : Window
    {
        public MessageClient ClientService { get; set; }

        private Dictionary<Guid, ChatWindow> ChatWindows { get; set; }

        public MainWindow()
        {
            InitializeComponent();

            this.ChatWindows = new Dictionary<Guid, ChatWindow>();

            _UpdateGUI();

            // Populate profile image list.
            LoadProfileImages();

            // Select random username for testing.
            tbxUsername.Text = Utility.GetRandomName();
        }

        public class ProfileImageEntry
        {
            public string ImagePath { get; set; }
            public string ImageName { get; set; }
        }

        private void LoadProfileImages()
        {
            string[] files = Directory.GetFiles(".\\resource_images\\", "profile_*.png");
            var imageList = new List<ProfileImageEntry>();
            foreach (var file in files)
            {
                var image = new ProfileImageEntry
                {
                    ImagePath = System.IO.Path.GetFullPath(file),
                    ImageName = System.IO.Path.GetFileName(file)
                };
                imageList.Add(image);
            }

            this.listBox_ProfileImages.DataContext = imageList;
        }

        private void _Connect()
        {
            if (ClientService != null && ClientService.Connected)
            {   // Already connected.
                return;
            }

            int profileImage = this.listBox_ProfileImages.SelectedIndex;
            if (profileImage == -1) profileImage = 0;

            pgbConnect.IsIndeterminate = true;

            IPAddress serverIP;
            if (IPAddress.TryParse(tbxServerAddress.Text.Trim(), out serverIP))
            {
                ClientService = new MessageClient(serverIP, profileImage);
                ClientService.DisplayName = tbxUsername.Text.Trim();

                // Initialize event handlers.
                ClientService.ConnectionEstablished += ClientService_ConnectionEstablished;
                ClientService.ConnectionFailed += ClientService_ConnectionFailed;
                ClientService.ServerDisconnected += ClientService_ServerDisconnected;
                ClientService.ClientDisconnected += ClientService_ClientDisconnected;

                ClientService.MessageReceived += ClientService_MessageReceived;
                ClientService.MessageSent += ClientService_MessageSent;
                ClientService.MessageReceivingingFailed += ClientService_MessageReceivingingFailed;
                ClientService.MessageSendingFailed += ClientService_MessageSendingFailed;
                ClientService.PrivateMessageReceived += ClientService_PrivateMessageReceived;

                ClientService.FileAvailable += ClientService_FileAvailable;
                ClientService.FileListReceived += ClientService_FileListReceived;
                ClientService.ReadyToUploadFile += ClientService_ReadyToUploadFile;

                ClientService.KnownChatroomsUpdated += ClientService_KnownChatroomsUpdated;
                ClientService.KnownClientsUpdated += ClientService_KnownClientsUpdated;

                ClientService.ClientJoinedChatroom += ClientService_ClientJoinedChatroom;
                ClientService.ClientLeftChatroom += ClientService_ClientLeftChatroom;

                ClientService.ConnectToServer();
            }
            else
            {   // Invalid IP address.
                ClientService_ConnectionFailed(this, null);
            }
        }

        private void ClientService_ReadyToUploadFile(object sender, MessageEventArgs e)
        {
            Guid roomId = e.Message.SenderId;
            int port = int.Parse(e.Message.Text);
            if (ChatWindows.ContainsKey(roomId))
                ChatWindows[roomId].BeginUpload(port);
        }

        private void ClientService_FileListReceived(object sender, MessageEventArgs e)
        {
            string fileList = e.Message.Text;
            Guid chatId = e.Message.SenderId;

            var list = new List<FileListLine>();

            if (fileList != null && fileList.Length > 0)
                using (var reader = new StringReader(fileList))
                {
                    string fileName;
                    while ((fileName = reader.ReadLine()) != null)
                    {
                        string sizeStr = reader.ReadLine();
                        string timeStr = reader.ReadLine();
                        string uploader = reader.ReadLine();

                        var size = long.Parse(sizeStr);
                        var time = DateTime.FromBinary(long.Parse(timeStr));

                        var fileEntry = new FileListLine(fileName, size, time, uploader);
                        list.Add(fileEntry);
                    }
                }

            if (ChatWindows.ContainsKey(chatId))
                ChatWindows[chatId].UpdateFileList(list);
        }

        private void ClientService_FileAvailable(object sender, MessageEventArgs e)
        {
            string[] fileInfo = e.Message.Text.Split('\n');
            // TODO: add to GUI file list
            string uploader = ClientService.GetClientName(e.Message.SenderId);

            //bool privateChat = ClientService.KnownClients.ContainsKey(e.Message.SenderId);
            bool privateChat = false;

            Message notification = new Message
            {
                Type = MessageType.SystemMessage,
                ControlInfo = ControlInfo.None,
                //SenderId = privateChat ? e.Message.SenderId : Message.NullId,
                SenderId = ProtocolSettings.NullId,
                TargetId = e.Message.TargetId,
                TimeSent = DateTime.Now,
                Text = "Client '" + uploader + "' has uploaded '" + fileInfo[0].Trim() + "'."
            };

            if (privateChat)
            {
                ClientService_PrivateMessageReceived(sender, new MessageEventArgs(notification));
                if (ChatWindows.ContainsKey(e.Message.SenderId))
                    ChatWindows[e.Message.SenderId].AddFileToList(new FileListLine(
                        fileInfo[0].Trim(),
                        long.Parse(fileInfo[1].Trim()),
                        DateTime.FromBinary(long.Parse(fileInfo[2].Trim())),
                        uploader));
            }
            else
                _DisplayMessage(notification);
        }

        private void _Disconnect()
        {
            ClientService.ConnectionEstablished -= ClientService_ConnectionEstablished;
            ClientService.ConnectionFailed -= ClientService_ConnectionFailed;
            ClientService.ServerDisconnected -= ClientService_ServerDisconnected;
            ClientService.ClientDisconnected -= ClientService_ClientDisconnected;

            ClientService.MessageReceived -= ClientService_MessageReceived;
            ClientService.MessageSent -= ClientService_MessageSent;
            ClientService.MessageReceivingingFailed -= ClientService_MessageReceivingingFailed;
            ClientService.MessageSendingFailed -= ClientService_MessageSendingFailed;
            ClientService.PrivateMessageReceived -= ClientService_PrivateMessageReceived;

            ClientService.FileAvailable -= ClientService_FileAvailable;
            ClientService.FileListReceived -= ClientService_FileListReceived;
            ClientService.ReadyToUploadFile -= ClientService_ReadyToUploadFile;

            ClientService.KnownChatroomsUpdated -= ClientService_KnownChatroomsUpdated;
            ClientService.KnownClientsUpdated -= ClientService_KnownClientsUpdated;

            ClientService.ClientJoinedChatroom -= ClientService_ClientJoinedChatroom;
            ClientService.ClientLeftChatroom -= ClientService_ClientLeftChatroom;

            // Remove all windows.
            foreach (var window in ChatWindows.Values)
            {
                window.Close();
            }
            ChatWindows.Clear();

            _UpdateGUI();
        }

        private void ClientService_PrivateMessageReceived(object sender, MessageEventArgs e)
        {
            this.Dispatcher.Invoke(() =>
            {   // Called from different thread.

                Guid chatId = e.Message.SenderId;
                if (chatId == ClientService.ClientId)  // The message that was mirrored back.
                    chatId = e.Message.TargetId;

                if (!ChatWindows.ContainsKey(chatId))
                {
                    ChatWindow win = new ChatWindow(chatId, this.ClientService, true);
                    win.ChatName = ClientService.GetClientName(chatId);
                    win.Show();
                    win.DisplayMessage(e.Message);

                    ChatWindows.Add(chatId, win);
                }
                else
                {
                    ChatWindows[chatId].DisplayMessage(e.Message);
                }
            });
        }

        private void ClientService_ClientLeftChatroom(object sender, ChatroomEventArgs e)
        {
            this.Dispatcher.Invoke(() =>
            {   // Called from different thread.
                if (ChatWindows.ContainsKey(e.ChatroomId))
                {
                    ChatWindows[e.ChatroomId].Close();
                    ChatWindows.Remove(e.ChatroomId);
                }
            });
        }

        private void ClientService_ClientJoinedChatroom(object sender, ChatroomEventArgs e)
        {
            this.Dispatcher.Invoke(() =>
            {   // Called from different thread.
                if (!ChatWindows.ContainsKey(e.ChatroomId))
                {
                    ChatWindow newWindow = new ChatWindow(e.ChatroomId, this.ClientService);
                    newWindow.ChatName = e.ChatroomName;
                    newWindow.LoadMemberList();
                    ChatWindows.Add(e.ChatroomId, newWindow);
                    newWindow.Show();
                }

                _DisplayMessage(new Message
                {
                    Type = MessageType.Control,
                    SenderId = ProtocolSettings.NullId,
                    TargetId = e.ChatroomId,
                    TimeSent = DateTime.Now,
                    Text = "You are now a member of this chatroom."
                });
            });
        }

        private void ClientService_KnownClientsUpdated(object sender, EventArgs e)
        {
            this.Dispatcher.Invoke(() =>
            {   // Called from different thread.
                listBox_Users.ItemsSource = null;
                listBox_Users.ItemsSource = ClientService.KnownClients.Values;

                foreach (var window in ChatWindows.Values)
                    window.LoadMemberList();
                //foreach (var roomId in ClientService.JoinedChatrooms)
                //{
                //    ChatWindows[roomId].listBox_Members.ItemsSource = null;
                //    ChatWindows[roomId].listBox_Members.ItemsSource 
                //        = ClientService.KnownChatrooms[roomId].MembersInfo;
                //}

                int count = ClientService.KnownClients.Count;
                lblCountUser.Text = string.Format("{0} {1} currently online.",
                    count, count == 1 ? "user" : "users");
                ClientService_KnownChatroomsUpdated(sender, e);
            });
        }

        private void ClientService_KnownChatroomsUpdated(object sender, EventArgs e)
        {
            this.Dispatcher.Invoke(() =>
            {   // Called from different thread.
                listBox_Chatrooms.ItemsSource = null;
                listBox_Chatrooms.ItemsSource = ClientService.KnownChatrooms.Values;

                foreach (var roomId in ClientService.JoinedChatrooms)
                    ChatWindows[roomId].ChatName = ClientService.GetChatroomName(roomId);

                int count = ClientService.KnownChatrooms.Count;
                lblCountRoom.Text = string.Format("{0} {1} currently active.",
                    count, count == 1 ? "chatroom" : "chatrooms");
            });
        }

        private void ClientService_MessageSendingFailed(object sender, MessageEventArgs e)
        {
            return;
            throw new NotImplementedException();
        }

        private void ClientService_MessageReceivingingFailed(object sender, MessageEventArgs e)
        {
            return;
            throw new NotImplementedException();
        }

        private void ClientService_MessageSent(object sender, MessageEventArgs e)
        {
            return;
            throw new NotImplementedException();
        }

        private void ClientService_MessageReceived(object sender, MessageEventArgs e)
        {
            _DisplayMessage(e.Message);
        }

        private void ClientService_ClientDisconnected(object sender, ConnectionEventArgs e)
        {
            _Disconnect();
        }

        private void ClientService_ServerDisconnected(object sender, ConnectionEventArgs e)
        {
            _UpdateGUI(true);
        }

        private void ClientService_ConnectionFailed(object sender, ConnectionEventArgs e)
        {
            _UpdateGUI(true);
        }

        private void ClientService_ConnectionEstablished(object sender, ConnectionEventArgs e)
        {
            ClientService.BeginReceive();
            _UpdateGUI(true);
        }

        private void btnConnect_Click(object sender, RoutedEventArgs e)
        {
            string username = this.tbxUsername.Text.Trim();
            if (username == "<SERVER>" || username == "<YOU>")
            {
                MessageBox.Show("That username is not allowed. Please try something else.",
                    "Invalid Username", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            _Connect();
        }

        private void btnDisconnect_Click(object sender, RoutedEventArgs e)
        {
            ClientService.Disconnect();
        }

        private void _DisplayMessage(Message message)
        {   // Must display in the correct chatroom.
            if (ChatWindows.ContainsKey(message.TargetId))
                ChatWindows[message.TargetId].DisplayMessage(message);
            else
                Console.WriteLine(message.ToString());
        }

        private void _UpdateGUI(bool showConnectionLabel = false)
        {
            this.Dispatcher.Invoke(() =>
            {
                bool connected = ClientService?.Connected ?? false;

                lblConnectionStatus1.Visibility = showConnectionLabel && connected
                    ? Visibility.Visible : Visibility.Collapsed;
                lblConnectionStatus2.Visibility = showConnectionLabel && !connected
                    ? Visibility.Visible : Visibility.Collapsed;

                btnDisconnect.Visibility = connected ? Visibility.Visible : Visibility.Collapsed;

                tbxServerAddress.IsEnabled = !connected;
                tbxUsername.IsEnabled = !connected;
                this.listBox_ProfileImages.IsEnabled = !connected;
                btnConnect.IsEnabled = !connected;

                tab_Users.IsEnabled = connected;
                tab_Chatrooms.IsEnabled = connected;

                if (!connected)
                    tab_Connect.IsSelected = true;

                this.Title = connected ? "LoliChat - " + tbxUsername.Text.Trim() : "LoliChat 1.0";

                pgbConnect.IsIndeterminate = false;
                pgbConnect.Value = connected ? 100 : 0;

                this.imgUserProfile.Source = connected ? 
                    new BitmapImage(new Uri(this.ClientService.ProfileImagePath)) : null;
            });
        }

        private void listBox_Users_ItemMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount >= 2)
            {
                var userInfo = (ClientInfo) listBox_Users.SelectedItem;
                //if (userInfo.ClientId == ClientService.ClientId)
                //    return;  // No chatting with yourself.

                if (!ChatWindows.ContainsKey(userInfo.ClientId))
                {
                    ChatWindow win = new ChatWindow(userInfo.ClientId, this.ClientService, true);
                    win.ChatName = userInfo.DisplayName;
                    win.Show();

                    ChatWindows.Add(userInfo.ClientId, win);
                }
                else
                {
                    ChatWindows[userInfo.ClientId].Visibility = Visibility.Visible;
                    ChatWindows[userInfo.ClientId].Activate();
                }
            }
        }

        private void listBox_Chatrooms_ItemMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount >= 2)
            {
                //MessageBox.Show(listBox_Chatrooms.SelectedItem.ToString());

                var roomInfo = (ChatroomInfo) listBox_Chatrooms.SelectedItem;

                if (ChatWindows.ContainsKey(roomInfo.ChatroomId))
                {
                    ChatWindows[roomInfo.ChatroomId].Visibility = Visibility.Visible;
                    ChatWindows[roomInfo.ChatroomId].Activate();
                }
                else
                    ClientService.RequestJoinChatroom(roomInfo.ChatroomId);
            }
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void tbxUsername_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                btnConnect_Click(sender, e);
                e.Handled = true;
            }
        }

        private void btnNewRoom_Click(object sender, RoutedEventArgs e)
        {
            string roomName = tbxNewRoomName.Text.Trim();
            MessageBoxResult confirm = MessageBox.Show("Create new chatroom '" + roomName + "'?", "New Chatroom", MessageBoxButton.OKCancel, MessageBoxImage.Question);

            if (confirm != MessageBoxResult.OK)
                return;

            ClientService.RequestCreateChatroom(roomName);
        }
    }
}
