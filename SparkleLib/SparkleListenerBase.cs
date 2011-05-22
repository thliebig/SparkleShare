//   SparkleShare, an instant update workflow to Git.
//   Copyright (C) 2010  Hylke Bons <hylkebons@gmail.com>
//
//   This program is free software: you can redistribute it and/or modify
//   it under the terms of the GNU General Public License as published by
//   the Free Software Foundation, either version 3 of the License, or
//   (at your option) any later version.
//
//   This program is distributed in the hope that it will be useful,
//   but WITHOUT ANY WARRANTY; without even the implied warranty of
//   MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
//   GNU General Public License for more details.
//
//   You should have received a copy of the GNU General Public License
//   along with this program. If not, see <http://www.gnu.org/licenses/>.


using System;
using System.Collections.Generic;

namespace SparkleLib {

    public enum NotificationServerType
    {
        Own,
        Central
    }


    public class SparkleAnnouncement {

        public readonly string FolderIdentifier;
        public readonly string Message;


        public SparkleAnnouncement (string folder_identifier, string message)
        {
            FolderIdentifier = folder_identifier;
            Message          = message;
        }
    }


    public static class SparkleListenerFactory {

        private static List<SparkleListenerBase> listeners;

        public static SparkleListenerIrc CreateIrcListener (string server, string folder_identifier,
                                                            NotificationServerType type)
        {
            if (listeners == null)
                listeners = new List<SparkleListenerBase> ();

            // This is SparkleShare's centralized notification service.
            // Don't worry, we only use this server as a backup if you
            // don't have your own. All data needed to connect is hashed and
            // we don't store any personal information ever
            if (type == NotificationServerType.Central)
                server = "204.62.14.135";

            foreach (SparkleListenerBase listener in listeners) {
                if (listener.Server.Equals (server)) {
                    SparkleHelpers.DebugInfo ("ListenerFactory", "Refered to existing listener for " + server);
                    listener.AlsoListenTo (folder_identifier);
                    return (SparkleListenerIrc) listener;
                }
            }

            SparkleHelpers.DebugInfo ("ListenerFactory", "Issued new listener for " + server);
            listeners.Add (new SparkleListenerIrc (server, folder_identifier, type));
            return (SparkleListenerIrc) listeners [listeners.Count - 1];
        }
    }


    // A persistent connection to the server that
    // listens for change notifications
    public abstract class SparkleListenerBase {

        // We've connected to the server
        public event ConnectedEventHandler Connected;
        public delegate void ConnectedEventHandler ();

        // We've disconnected from the server
        public event DisconnectedEventHandler Disconnected;
        public delegate void DisconnectedEventHandler ();

        // We've been notified about a remote
        // change by the channel
        public event RemoteChangeEventHandler RemoteChange;
        public delegate void RemoteChangeEventHandler (SparkleAnnouncement announcement);


        public abstract void Connect ();
        public abstract void Announce (SparkleAnnouncement announcent);
        public abstract void AlsoListenTo (string folder_identifier);
        public abstract void Dispose ();
        public abstract bool IsConnected { get; }


        // Announcements that weren't sent off
        // because we were disconnected
        protected List<SparkleAnnouncement> announce_queue = new List<SparkleAnnouncement> ();
        protected string server;
        protected List<string> channels = new List<string> ();
        protected int changes_queue = 0;
        protected bool is_connecting;


        public SparkleListenerBase (string server, string folder_identifier, NotificationServerType type) { }


        public string Server {
            get {
                return this.server;
            }
        }


        // Announcements of remote changes that we've received
        public int ChangesQueue {
            get {
                return this.changes_queue;
            }
        }


        public bool IsConnecting {
            get {
               return this.is_connecting;
            }
        }


        public void AnnounceBase (SparkleAnnouncement announcement) {
            if (IsConnected) {
                SparkleHelpers.DebugInfo ("Listener", "Announcing to " + announcement.FolderIdentifier + " on " + this.server);
                Announce (announcement);
            } else {
                SparkleHelpers.DebugInfo ("Listener", "Not connected to " + this.server + ". Queuing message");
                this.announce_queue.Add (announcement);
            }
        }


        public void DecrementChangesQueue ()
        {
            this.changes_queue--;
        }


        public void OnConnected ()
        {
            SparkleHelpers.DebugInfo ("Listener", "Connected to " + Server);

            if (Connected != null)
                Connected ();

            if (this.announce_queue.Count > 0) {
                SparkleHelpers.DebugInfo ("Listener", "Delivering queued messages...");
                foreach (SparkleAnnouncement announcement in this.announce_queue)
                    AnnounceBase (announcement);

                this.announce_queue = new List<SparkleAnnouncement> ();
            }
        }


        public void OnDisconnected ()
        {
            SparkleHelpers.DebugInfo ("Listener", "Disonnected");

            if (Disconnected != null)
                Disconnected ();
        }


        public void OnRemoteChange (SparkleAnnouncement announcement)
        {
            SparkleHelpers.DebugInfo ("Listener", "Got message from " + announcement.FolderIdentifier + " on " + this.server);
 
            this.changes_queue++;

            if (RemoteChange != null)
                RemoteChange (announcement);
        }
    }
}