// 
// Radegast Metaverse Client
// Copyright (c) 2009, Radegast Development Team
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are met:
// 
//     * Redistributions of source code must retain the above copyright notice,
//       this list of conditions and the following disclaimer.
//     * Redistributions in binary form must reproduce the above copyright
//       notice, this list of conditions and the following disclaimer in the
//       documentation and/or other materials provided with the distribution.
//     * Neither the name of the application "Radegast", nor the names of its
//       contributors may be used to endorse or promote products derived from
//       this software without specific prior written permission.
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
// AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
// IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE
// FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL
// DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR
// SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER
// CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY,
// OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
// OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
//
// $Id$
//
﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using OpenMetaverse;

namespace Radegast
{
    public partial class InventoryConsole : UserControl
    {
        RadegastInstance instance;
        GridClient client { get { return instance.Client; } }
        Dictionary<UUID, TreeNode> FolderNodes = new Dictionary<UUID, TreeNode>();

        private InventoryManager Manager;
        private OpenMetaverse.Inventory Inventory;
        private TreeNode invRootNode;
        private string newItemName = string.Empty;
        private List<UUID> fetchedFolders = new List<UUID>();

        #region Construction and disposal
        public InventoryConsole(RadegastInstance instance)
        {
            InitializeComponent();
            Disposed += new EventHandler(InventoryConsole_Disposed);

            this.instance = instance;
            Manager = client.Inventory;
            Inventory = Manager.Store;
            Inventory.RootFolder.OwnerID = client.Self.AgentID;
            invTree.ImageList = frmMain.ResourceImages;
            invTree.AfterExpand += new TreeViewEventHandler(TreeView_AfterExpand);
            invTree.MouseClick += new MouseEventHandler(invTree_MouseClick);
            invTree.NodeMouseDoubleClick += new TreeNodeMouseClickEventHandler(invTree_NodeMouseDoubleClick);
            invTree.TreeViewNodeSorter = new InvNodeSorter();
            invRootNode = AddDir(null, Inventory.RootFolder);
            invTree.Nodes[0].Expand();

            // Callbacks
            client.Avatars.OnAvatarNames += new AvatarManager.AvatarNamesCallback(Avatars_OnAvatarNames);
            client.Inventory.Store.OnInventoryObjectAdded += new Inventory.InventoryObjectAdded(Store_OnInventoryObjectAdded);
            client.Inventory.Store.OnInventoryObjectUpdated += new Inventory.InventoryObjectUpdated(Store_OnInventoryObjectUpdated);
            client.Inventory.Store.OnInventoryObjectRemoved += new Inventory.InventoryObjectRemoved(Store_OnInventoryObjectRemoved);

        }

        void InventoryConsole_Disposed(object sender, EventArgs e)
        {
            client.Avatars.OnAvatarNames -= new AvatarManager.AvatarNamesCallback(Avatars_OnAvatarNames);
            client.Inventory.Store.OnInventoryObjectAdded -= new Inventory.InventoryObjectAdded(Store_OnInventoryObjectAdded);
            client.Inventory.Store.OnInventoryObjectUpdated -= new Inventory.InventoryObjectUpdated(Store_OnInventoryObjectUpdated);
            client.Inventory.Store.OnInventoryObjectRemoved -= new Inventory.InventoryObjectRemoved(Store_OnInventoryObjectRemoved);
        }
        #endregion

        #region Network callbacks
        void Store_OnInventoryObjectAdded(InventoryBase obj)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new MethodInvoker(delegate()
                    {
                        Store_OnInventoryObjectAdded(obj);
                    }
                ));
                return;
            }

            Logger.DebugLog("Inv created:" + obj.Name + ": " + obj.UUID);

            TreeNode parent = findNodeForItem(invRootNode, obj.ParentUUID);

            if (parent != null)
            {
                TreeNode newNode = AddBase(parent, obj);
                if (obj.Name == newItemName)
                {
                    if (newNode.Parent.IsExpanded)
                    {
                        newNode.BeginEdit();
                    }
                    else
                    {
                        newNode.Parent.Expand();
                    }
                }
            }
            newItemName = string.Empty;
        }

        void Store_OnInventoryObjectRemoved(InventoryBase obj)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new MethodInvoker(delegate()
                {
                    Store_OnInventoryObjectRemoved(obj);
                }
                ));
                return;
            }

            Logger.DebugLog("Inv removed:" + obj.Name);

            TreeNode currentNode = findNodeForItem(invRootNode, obj.UUID);
            if (currentNode != null)
            {
                currentNode.Remove();
            }
        }

        void Store_OnInventoryObjectUpdated(InventoryBase oldObject, InventoryBase newObject)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new MethodInvoker(delegate()
                {
                    Store_OnInventoryObjectUpdated(oldObject, newObject);
                }
                ));
                return;
            }

            Logger.DebugLog("Inv updated:" + newObject.Name);

            // Find our current node in the tree
            TreeNode currentNode = findNodeForItem(invRootNode, newObject.UUID);

            // Find which node should be our parrent
            TreeNode parent = findNodeForItem(invRootNode, newObject.ParentUUID);

            if (parent == null) return;

            if (currentNode != null)
            {
                // Did we move to a different folder
                if (currentNode.Parent != parent)
                {
                    currentNode.Remove();
                    AddBase(parent, newObject);
                }
                else // Update
                {
                    currentNode.Tag = newObject;
                    currentNode.Text = newObject.Name;
                    currentNode.Name = newObject.Name;
                }
            }
            else // We are not in the tree already, add
            {
                AddBase(parent, newObject);
            }

        }


        void Avatars_OnAvatarNames(Dictionary<UUID, string> names)
        {
            if (txtCreator.Tag == null) return;
            if (InvokeRequired)
            {
                BeginInvoke(new MethodInvoker(delegate()
                {
                    Avatars_OnAvatarNames(names);
                }));
                return;
            }

            if (names.ContainsKey((UUID)txtCreator.Tag))
            {
                txtCreator.Text = names[(UUID)txtCreator.Tag];
            }
        }
        #endregion

        private void btnProfile_Click(object sender, EventArgs e)
        {
            (new frmProfile(instance, txtCreator.Text, (UUID)txtCreator.Tag)).Show();
            
        }

        void UpdateItemInfo(InventoryItem item)
        {
            btnProfile.Enabled = true;
            txtItemName.Text = item.Name;
            txtCreator.Text = instance.getAvatarName(item.CreatorID);
            txtCreator.Tag = item.CreatorID;
            txtCreated.Text = item.CreationDate.ToString();

            if (item.AssetUUID != null && item.AssetUUID != UUID.Zero)
            {
                txtAssetID.Text = item.AssetUUID.ToString();
            }
            else
            {
                txtAssetID.Text = String.Empty;
            }
            pnlDetail.Controls.Clear();

            switch (item.AssetType)
            {
                case AssetType.Texture:
                    SLImageHandler image = new SLImageHandler(instance, item.AssetUUID, item.Name);
                    image.Dock = DockStyle.Fill;
                    pnlDetail.Controls.Add(image);
                    break;
                
                case AssetType.Notecard:
                    Notecard note = new Notecard(instance, (InventoryNotecard)item);
                    note.Dock = DockStyle.Fill;
                    pnlDetail.Controls.Add(note);
                    break;
            }
        }

        void invTree_NodeMouseDoubleClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            if (e.Node.Tag is InventoryItem)
            {
                InventoryItem item = e.Node.Tag as InventoryItem;
                switch (item.AssetType)
                {
                        
                    case AssetType.Landmark:
                        instance.TabConsole.DisplayNotificationInChat("Teleporting to " + item.Name);
                        client.Self.RequestTeleport(item.AssetUUID);
                        break;
                }
            }
        }

        private void fetchFolder(UUID folderID, UUID ownerID, bool force)
        {
            if (force || !fetchedFolders.Contains(folderID))
            {
                if (!fetchedFolders.Contains(folderID))
                {
                    fetchedFolders.Add(folderID);
                }
                client.Inventory.RequestFolderContents(folderID, ownerID, true, true, InventorySortOrder.ByDate);
            }
        }

        void invTree_MouseClick(object sender, MouseEventArgs e)
        {
            TreeNode node = invTree.GetNodeAt(new Point(e.X, e.Y));
            if (node == null) return;

            if (e.Button == MouseButtons.Left)
            {
                invTree.SelectedNode = node;
                if (node.Tag is InventoryItem)
                {
                    UpdateItemInfo(node.Tag as InventoryItem);
                }
            }
            else if (e.Button == MouseButtons.Right)
            {
                invTree.SelectedNode = node;
                if (node.Tag is InventoryFolder)
                {
                    InventoryFolder folder = (InventoryFolder)node.Tag;
                    fetchFolder(folder.UUID, folder.OwnerID, false);
                    ctxInv.Items.Clear();

                    ToolStripMenuItem ctxItem;

                    ctxItem = new ToolStripMenuItem("New folder", null, OnInvContextClick);
                    ctxItem.Name = "new_folder";
                    ctxInv.Items.Add(ctxItem);

                    ctxItem = new ToolStripMenuItem("Refresh", null, OnInvContextClick);
                    ctxItem.Name = "refresh";
                    ctxInv.Items.Add(ctxItem);

                    ctxInv.Items.Add(new ToolStripSeparator());

                    ctxItem = new ToolStripMenuItem("Expand", null, OnInvContextClick);
                    ctxItem.Name = "expand";
                    ctxInv.Items.Add(ctxItem);

                    ctxItem = new ToolStripMenuItem("Expand all", null, OnInvContextClick);
                    ctxItem.Name = "expand_all";
                    ctxInv.Items.Add(ctxItem);

                    ctxItem = new ToolStripMenuItem("Collapse", null, OnInvContextClick);
                    ctxItem.Name = "collapse";
                    ctxInv.Items.Add(ctxItem);

                    if (folder.PreferredType == AssetType.TrashFolder)
                    {
                        ctxItem = new ToolStripMenuItem("Empty trash", null, OnInvContextClick);
                        ctxItem.Name = "empty_trash";
                        ctxInv.Items.Add(ctxItem);
                    }

                    if (folder.PreferredType == AssetType.LostAndFoundFolder)
                    {
                        ctxItem = new ToolStripMenuItem("Empty lost and found", null, OnInvContextClick);
                        ctxItem.Name = "empty_lost_found";
                        ctxInv.Items.Add(ctxItem);
                    }

                    if (folder.PreferredType == AssetType.Unknown)
                    {
                        ctxItem = new ToolStripMenuItem("Rename", null, OnInvContextClick);
                        ctxItem.Name = "rename_folder";
                        ctxInv.Items.Add(ctxItem);
                    }


                    if (folder.PreferredType == AssetType.Unknown)
                    {
                        ctxInv.Items.Add(new ToolStripSeparator());
                        ctxItem = new ToolStripMenuItem("Delete", null, OnInvContextClick);
                        ctxItem.Name = "delete_folder";
                        ctxInv.Items.Add(ctxItem);
                    }

                    ctxInv.Show(invTree, new Point(e.X, e.Y));
                }
                else if (node.Tag is InventoryItem)
                {
                    InventoryItem item = (InventoryItem)node.Tag;
                    ctxInv.Items.Clear();

                    ToolStripMenuItem ctxItem;

                    ctxItem = new ToolStripMenuItem("Rename", null, OnInvContextClick);
                    ctxItem.Name = "rename_item";
                    ctxInv.Items.Add(ctxItem);

                    ctxInv.Items.Add(new ToolStripSeparator());

                    ctxItem = new ToolStripMenuItem("Delete", null, OnInvContextClick);
                    ctxItem.Name = "delete_item";
                    ctxInv.Items.Add(ctxItem);

                    ctxInv.Show(invTree, new Point(e.X, e.Y));

                }
                Logger.Log("Right click on node: " + node.Name, Helpers.LogLevel.Debug, client);
            }
        }

        #region Context menu folder
        private void OnInvContextClick(object sender, EventArgs e)
        {
            if (invTree.SelectedNode == null || !(invTree.SelectedNode.Tag is InventoryBase))
            {
                return;
            }

            string cmd = ((ToolStripMenuItem)sender).Name;

            if (invTree.SelectedNode.Tag is InventoryFolder)
            {
                InventoryFolder f = (InventoryFolder)invTree.SelectedNode.Tag;

                switch (cmd)
                {
                    case "refresh":
                        invTree.SelectedNode.Nodes.Clear();
                        fetchFolder(f.UUID, f.OwnerID, true);
                        break;

                    case "expand":
                        invTree.SelectedNode.Expand();
                        break;

                    case "expand_all":
                        invTree.SelectedNode.ExpandAll();
                        break;

                    case "collapse":
                        invTree.SelectedNode.Collapse();
                        break;

                    case "new_folder":
                        newItemName = "New folder";
                        client.Inventory.CreateFolder(f.UUID, "New folder");
                        break;

                    case "delete_folder":
                        client.Inventory.MoveFolder(f.UUID, client.Inventory.FindFolderForType(AssetType.TrashFolder), f.Name);
                        // invTree.SelectedNode.Remove();
                        break;

                    case "empty_trash":
                        {
                            DialogResult res = MessageBox.Show("Are you sure you want to empty your trash?", "Confirmation", MessageBoxButtons.OKCancel);
                            if (res == DialogResult.OK)
                            {
                                client.Inventory.EmptyTrash();
                            }
                        }
                        break;

                    case "empty_lost_found":
                        {
                            DialogResult res = MessageBox.Show("Are you sure you want to empty your lost and found folder?", "Confirmation", MessageBoxButtons.OKCancel);
                            if (res == DialogResult.OK)
                            {
                                client.Inventory.EmptyLostAndFound();
                            }
                        }
                        break;

                    case "rename_folder":
                        invTree.SelectedNode.BeginEdit();
                        break;

                }
            }
            else if (invTree.SelectedNode.Tag is InventoryItem)
            {
                InventoryItem item = (InventoryItem)invTree.SelectedNode.Tag;

                switch(cmd)
                {
                    case "delete_item":
                        client.Inventory.MoveItem(item.UUID, client.Inventory.FindFolderForType(AssetType.TrashFolder), item.Name);
                        // invTree.SelectedNode.Remove();
                        break;

                    case "rename_item":
                        invTree.SelectedNode.BeginEdit();
                        break;
                }
            }
        }
        #endregion


        void TreeView_AfterExpand(object sender, TreeViewEventArgs e)
        {
            TreeNode node = e.Node;
            InventoryFolder f = (InventoryFolder)node.Tag;
            if (f == null)
            {
                return;
            }

            fetchFolder(f.UUID, f.OwnerID, false);

            try
            {
                List<InventoryBase> contents = client.Inventory.Store.GetContents(f);
                foreach (InventoryBase item in contents)
                {
                    UpdateBase(node, item);
                }
            }
            catch (Exception)
            {
                fetchFolder(f.UUID, f.OwnerID, true);
            }

            TreeNode dummy = null;
            foreach (TreeNode n in node.Nodes)
            {
                if (n.Name == "DummyTreeNode")
                {
                    dummy = n;
                    break;
                }
            }

            if (dummy != null)
            {
                dummy.Remove();
            }

            // Check if we need to go into edit mode for new items
            if (newItemName != string.Empty)
            {
                foreach (TreeNode n in node.Nodes)
                {
                    if (n.Name == newItemName)
                    {
                        n.BeginEdit();
                        break;
                    }
                }
                newItemName = string.Empty;
            }
        }

        private void invTree_BeforeLabelEdit(object sender, NodeLabelEditEventArgs e)
        {
            if (e.Node != null &&
                e.Node.Tag is InventoryFolder &&
                ((InventoryFolder)e.Node.Tag).PreferredType != AssetType.Unknown)
            {
                e.CancelEdit = true;
            }
        }

        private void invTree_AfterLabelEdit(object sender, NodeLabelEditEventArgs e)
        {
            if (string.IsNullOrEmpty(e.Label))
            {
                e.CancelEdit = true;
                return;
            }

            if (e.Node.Tag is InventoryFolder)
            {
                InventoryFolder f = (InventoryFolder)e.Node.Tag;
                f.Name = e.Label;
                client.Inventory.MoveFolder(f.UUID, f.ParentUUID, f.Name);
            }
            else if (e.Node.Tag is InventoryItem)
            {
                InventoryItem item = (InventoryItem)e.Node.Tag;
                item.Name = e.Label;
                client.Inventory.MoveItem(item.UUID, item.ParentUUID, item.Name);
            }

        }

        private void invTree_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.F2 && invTree.SelectedNode != null)
            {
                invTree.SelectedNode.BeginEdit();
            }
        }

        #region Drag and Drop
        private void invTree_ItemDrag(object sender, ItemDragEventArgs e)
        {
            invTree.SelectedNode = e.Item as TreeNode;
            if (invTree.SelectedNode.Tag is InventoryFolder && ((InventoryFolder)invTree.SelectedNode.Tag).PreferredType != AssetType.Unknown)
            {
                return;
            }
            invTree.DoDragDrop(e.Item, DragDropEffects.Move);
        }

        private void invTree_DragDrop(object sender, DragEventArgs e)
        {
            if (highlightedNode != null)
            {
                highlightedNode.BackColor = invTree.BackColor;
                highlightedNode = null;
            }

            TreeNode sourceNode = e.Data.GetData(typeof(TreeNode)) as TreeNode;
            if (sourceNode == null) return;

            Point pt = ((TreeView)sender).PointToClient(new Point(e.X, e.Y));
            TreeNode destinationNode = ((TreeView)sender).GetNodeAt(pt);

            if (destinationNode == null) return;

            if (sourceNode == destinationNode) return;

            // If droping to item within folder drop to its folder
            if (destinationNode.Tag is InventoryItem)
            {
                destinationNode = destinationNode.Parent;
            }

            InventoryFolder dest = destinationNode.Tag as InventoryFolder;

            if (dest == null) return;

            if (sourceNode.Tag is InventoryItem)
            {
                InventoryItem item = (InventoryItem)sourceNode.Tag;
                client.Inventory.MoveItem(item.UUID, dest.UUID, item.Name);
                // sourceNode.Remove();
            }
            else if (sourceNode.Tag is InventoryFolder)
            {
                InventoryFolder f = (InventoryFolder)sourceNode.Tag;
                client.Inventory.MoveFolder(f.UUID, dest.UUID, f.Name);
                // sourceNode.Remove();
            }
        }

        private void invTree_DragEnter(object sender, DragEventArgs e)
        {
            TreeNode node = e.Data.GetData(typeof(TreeNode)) as TreeNode;
            if (node == null)
            {
                e.Effect = DragDropEffects.None;
            }
            else
            {
                e.Effect = DragDropEffects.Move;
            }
        }

        TreeNode highlightedNode = null;

        private void invTree_DragOver(object sender, DragEventArgs e)
        {
            TreeNode node = e.Data.GetData(typeof(TreeNode)) as TreeNode;
            if (node == null)
            {
                e.Effect = DragDropEffects.None;
            }

            Point pt = ((TreeView)sender).PointToClient(new Point(e.X, e.Y));
            TreeNode destinationNode = ((TreeView)sender).GetNodeAt(pt);

            if (highlightedNode != destinationNode)
            {
                if (highlightedNode != null)
                {
                    highlightedNode.BackColor = invTree.BackColor;
                    highlightedNode = null;
                }

                highlightedNode = destinationNode;
                highlightedNode.BackColor = Color.LightSlateGray;
            }

            if (destinationNode == null)
            {
                e.Effect = DragDropEffects.None;
                return;
            }

            e.Effect = DragDropEffects.Move;

        }
        #endregion

    }

    #region Sorter class
    // Create a node sorter that implements the IComparer interface.
    public class InvNodeSorter : IComparer
    {
        bool _sysfirst = true;

        int CompareFolders(InventoryFolder x, InventoryFolder y)
        {
            if (_sysfirst)
            {
                if (x.PreferredType != AssetType.Unknown && y.PreferredType == AssetType.Unknown)
                {
                    return -1;
                }
                else if (x.PreferredType == AssetType.Unknown && y.PreferredType != AssetType.Unknown)
                {
                    return 1;
                }
            }
            return String.Compare(x.Name, y.Name);
        }

        public bool SystemFoldersFirst { set { _sysfirst = value; } get { return _sysfirst; } }

        public int Compare(object x, object y)
        {
            TreeNode tx = x as TreeNode;
            TreeNode ty = y as TreeNode;

            if (tx.Tag is InventoryFolder && ty.Tag is InventoryFolder)
            {
                return CompareFolders(tx.Tag as InventoryFolder, ty.Tag as InventoryFolder);
            }
            else if (tx.Tag is InventoryFolder && ty.Tag is InventoryItem)
            {
                return -1;
            }
            else if (tx.Tag is InventoryItem && ty.Tag is InventoryFolder)
            {
                return 1;
            }

            // Two items
            if (!(tx.Tag is InventoryItem) || !(ty.Tag is InventoryItem))
            {
                return 0;
            }
            InventoryItem item1 = (InventoryItem)tx.Tag;
            InventoryItem item2 = (InventoryItem)ty.Tag;
            if (item1.CreationDate < item2.CreationDate)
            {
                return 1;
            }
            else if (item1.CreationDate > item2.CreationDate)
            {
                return -1;
            }
            return string.Compare(item1.Name, item2.Name);
        }
    }
    #endregion
}