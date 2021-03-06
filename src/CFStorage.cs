﻿using System;
using System.Collections.Generic;
using System.Text;
using RedBlackTree;
using System.Diagnostics;

/*
     The contents of this file are subject to the Mozilla Public License
     Version 1.1 (the "License"); you may not use this file except in
     compliance with the License. You may obtain a copy of the License at
     http://www.mozilla.org/MPL/

     Software distributed under the License is distributed on an "AS IS"
     basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See the
     License for the specific language governing rights and limitations
     under the License.

     The Original Code is OpenMCDF - Compound Document Format library.

     The Initial Developer of the Original Code is Federico Blaseotto.
*/

namespace OpenMcdf
{
    /// <summary>
    /// Action to apply to  visited items in the OLE structured storage
    /// </summary>
    /// <param name="item">Currently visited <see cref="T:OpenMcdf.CFItem">item</see></param>
    /// <example>
    /// <code>
    /// 
    /// //We assume that xls file should be a valid OLE compound file
    /// const String STORAGE_NAME = "report.xls";
    /// CompoundFile cf = new CompoundFile(STORAGE_NAME);
    ///
    /// FileStream output = new FileStream("LogEntries.txt", FileMode.Create);
    /// TextWriter tw = new StreamWriter(output);
    ///
    /// VisitedEntryAction va = delegate(CFItem item)
    /// {
    ///     tw.WriteLine(item.Name);
    /// };
    ///
    /// cf.RootStorage.VisitEntries(va, true);
    ///
    /// tw.Close();
    ///
    /// </code>
    /// </example>
    public delegate void VisitedEntryAction(CFItem item);

    /// <summary>
    /// Storage entity that acts like a logic container for streams
    /// or substorages in a compound file.
    /// </summary>
    public class CFStorage : CFItem, ICFStorage
    {
        private RBTree<CFItem> children;

        internal RBTree<CFItem> Children
        {
            get
            {
                // Lazy loading of children tree.
                if (children == null)
                {
                    if (this.CompoundFile.HasSourceStream)
                    {
                        children = LoadChildren(this.DirEntry.SID);
                    }
                    else
                    {
                        children = this.CompoundFile.CreateNewTree();
                    }
                }

                return children;
            }
        }

        /// <summary>
        /// Create a new CFStorage
        /// </summary>
        /// <param name="compFile">The Storage Owner - CompoundFile</param>
        internal CFStorage(CompoundFile compFile)
            : base(compFile)
        {
            this.DirEntry = new DirectoryEntry(StgType.StgStorage);
            this.DirEntry.StgColor = StgColor.Black;
            compFile.InsertNewDirectoryEntry(this.DirEntry);
        }

        /// <summary>
        /// Create a CFStorage using an existing directory (previously loaded).
        /// </summary>
        /// <param name="compFile">The Storage Owner - CompoundFile</param>
        /// <param name="dirEntry">An existing Directory Entry</param>
        internal CFStorage(CompoundFile compFile, IDirectoryEntry dirEntry)
            : base(compFile)
        {
            if (dirEntry == null || dirEntry.SID < 0)
                throw new CFException("Attempting to create a CFStorage using an unitialized directory");

            this.DirEntry = dirEntry;
        }

        private RBTree<CFItem> LoadChildren(int SID)
        {
            RBTree<CFItem> childrenTree = this.CompoundFile.GetChildrenTree(SID);

            if (childrenTree.Root != null)
                this.DirEntry.Child = childrenTree.Root.Value.DirEntry.SID;
            else
                this.DirEntry.Child = DirectoryEntry.NOSTREAM;

            return childrenTree;
        }

        /// <summary>
        /// Create a new child stream inside the current <see cref="T:OpenMcdf.CFStorage">storage</see>
        /// </summary>
        /// <param name="streamName">The new stream name</param>
        /// <returns>The new <see cref="T:OpenMcdf.CFStream">stream</see> reference</returns>
        /// <exception cref="T:OpenMcdf.CFDuplicatedItemException">Raised when adding an item with the same name of an existing one</exception>
        /// <exception cref="T:OpenMcdf.CFDisposedException">Raised when adding a stream to a closed compound file</exception>
        /// <exception cref="T:OpenMcdf.CFException">Raised when adding a stream with null or empty name</exception>
        /// <example>
        /// <code>
        /// 
        ///  String filename = "A_NEW_COMPOUND_FILE_YOU_CAN_WRITE_TO.cfs";
        ///
        ///  CompoundFile cf = new CompoundFile();
        ///
        ///  CFStorage st = cf.RootStorage.AddStorage("MyStorage");
        ///  CFStream sm = st.AddStream("MyStream");
        ///  byte[] b = Helpers.GetBuffer(220, 0x0A);
        ///  sm.SetData(b);
        ///
        ///  cf.Save(filename);
        ///  
        /// </code>
        /// </example>
        public ICFStream AddStream(String streamName)
        {
            CheckDisposed();

            if (String.IsNullOrEmpty(streamName))
                throw new CFException("Stream name cannot be null or empty");

            CFStream cfo = null;


            // Add new Stream directory entry
            cfo = new CFStream(this.CompoundFile);
            cfo.DirEntry.SetEntryName(streamName);


            try
            {
                // Add object to Siblings tree
                this.Children.Insert(cfo);
                //Trace.WriteLine("**** INSERT STREAM " + cfo.Name + "******");
                //this.Children.Print();
                //Rethread children tree...
                // CompoundFile.RefreshIterative(Children.Root);

                //... and set the root of the tree as new child of the current item directory entry
                this.DirEntry.Child = Children.Root.Value.DirEntry.SID;
            }
            catch (RBTreeException)
            {
                CompoundFile.ResetDirectoryEntry(cfo.DirEntry.SID);
                cfo = null;
                throw new CFDuplicatedItemException("An entry with name '" + streamName + "' is already present in storage '" + this.Name + "' ");
            }

            return cfo; // as ICFStream;
        }


        /// <summary>
        /// Get a named <see cref="T:OpenMcdf.CFStream">stream</see> contained in the current storage if existing.
        /// </summary>
        /// <param name="streamName">Name of the stream to look for</param>
        /// <returns>A stream reference if existing</returns>
        /// <exception cref="T:OpenMcdf.CFDisposedException">Raised if trying to delete item from a closed compound file</exception>
        /// <exception cref="T:OpenMcdf.CFItemNotFound">Raised if item to delete is not found</exception>
        /// <example>
        /// <code>
        /// String filename = "report.xls";
        ///
        /// CompoundFile cf = new CompoundFile(filename);
        /// CFStream foundStream = cf.RootStorage.GetStream("Workbook");
        ///
        /// byte[] temp = foundStream.GetData();
        ///
        /// Assert.IsNotNull(temp);
        ///
        /// cf.Close();
        /// </code>
        /// </example>
        public ICFStream GetStream(String streamName)
        {
            CheckDisposed();

            CFMock tmp = new CFMock(streamName, StgType.StgStream);

            //if (children == null)
            //{
            //    children = compoundFile.GetChildrenTree(SID);
            //}

            CFItem outDe = null;

            if (Children.TryLookup(tmp, out outDe) && outDe.DirEntry.StgType == StgType.StgStream)
            {
                return outDe as ICFStream;
            }
            else
            {
                throw new CFItemNotFound("Cannot find item [" + streamName + "] within the current storage");
            }
        }


        /// <summary>
        /// Checks whether a child stream exists in the parent.
        /// </summary>
        /// <param name="streamName">Name of the stream to look for</param>
        /// <returns>A boolean value indicating whether the child stream exists.</returns>
        /// <example>
        /// <code>
        /// String filename = "report.xls";
        ///
        /// CompoundFile cf = new CompoundFile(filename);
        /// 
        /// bool exists = ExistsStream("Workbook");
        /// 
        /// if exists
        /// {
        ///     CFStream foundStream = cf.RootStorage.GetStream("Workbook");
        /// 
        ///     byte[] temp = foundStream.GetData();
        /// }
        ///
        /// Assert.IsNotNull(temp);
        ///
        /// cf.Close();
        /// </code>
        /// </example>
        public bool ExistsStream(string streamName)
        {
            CheckDisposed();

            var tmp = new CFMock(streamName, StgType.StgStream);

            CFItem outDe = null;
            return Children.TryLookup(tmp, out outDe) && outDe.DirEntry.StgType == StgType.StgStream;
        }


        /// <summary>
        /// Get a named storage contained in the current one if existing.
        /// </summary>
        /// <param name="storageName">Name of the storage to look for</param>
        /// <returns>A storage reference if existing.</returns>
        /// <exception cref="T:OpenMcdf.CFDisposedException">Raised if trying to delete item from a closed compound file</exception>
        /// <exception cref="T:OpenMcdf.CFItemNotFound">Raised if item to delete is not found</exception>
        /// <example>
        /// <code>
        /// 
        /// String FILENAME = "MultipleStorage2.cfs";
        /// CompoundFile cf = new CompoundFile(FILENAME, UpdateMode.ReadOnly, false, false);
        ///
        /// CFStorage st = cf.RootStorage.GetStorage("MyStorage");
        ///
        /// Assert.IsNotNull(st);
        /// cf.Close();
        /// </code>
        /// </example>
        public ICFStorage GetStorage(String storageName)
        {
            CheckDisposed();

            CFMock tmp = new CFMock(storageName, StgType.StgStorage);

            CFItem outDe = null;
            if (Children.TryLookup(tmp, out outDe) && outDe.DirEntry.StgType == StgType.StgStorage)
            {
                return outDe as CFStorage;
            }
            else
            {
                throw new CFItemNotFound("Cannot find item [" + storageName + "] within the current storage");
            }
        }


        /// <summary>
        /// Checks if a child storage exists within the parent.
        /// </summary>
        /// <param name="storageName">Name of the storage to look for.</param>
        /// <returns>A boolean value indicating whether the child storage was found.</returns>
        /// <example>
        /// <code>
        /// String FILENAME = "MultipleStorage2.cfs";
        /// CompoundFile cf = new CompoundFile(FILENAME, UpdateMode.ReadOnly, false, false);
        ///
        /// bool exists = cf.RootStorage.ExistsStorage("MyStorage");
        /// 
        /// if exists
        /// {
        ///     CFStorage st = cf.RootStorage.GetStorage("MyStorage");
        /// }
        /// 
        /// Assert.IsNotNull(st);
        /// cf.Close();
        /// </code>
        /// </example>
        public bool ExistsStorage(string storageName)
        {
            CheckDisposed();

            var tmp = new CFMock(storageName, StgType.StgStorage);

            CFItem outDe = null;
            return Children.TryLookup(tmp, out outDe) && outDe.DirEntry.StgType == StgType.StgStorage;
        }


        /// <summary>
        /// Create new child storage directory inside the current storage.
        /// </summary>
        /// <param name="storageName">The new storage name</param>
        /// <returns>Reference to the new <see cref="T:OpenMcdf.CFStorage">storage</see></returns>
        /// <exception cref="T:OpenMcdf.CFDuplicatedItemException">Raised when adding an item with the same name of an existing one</exception>
        /// <exception cref="T:OpenMcdf.CFDisposedException">Raised when adding a storage to a closed compound file</exception>
        /// <exception cref="T:OpenMcdf.CFException">Raised when adding a storage with null or empty name</exception>
        /// <example>
        /// <code>
        /// 
        ///  String filename = "A_NEW_COMPOUND_FILE_YOU_CAN_WRITE_TO.cfs";
        ///
        ///  CompoundFile cf = new CompoundFile();
        ///
        ///  CFStorage st = cf.RootStorage.AddStorage("MyStorage");
        ///  CFStream sm = st.AddStream("MyStream");
        ///  byte[] b = Helpers.GetBuffer(220, 0x0A);
        ///  sm.SetData(b);
        ///
        ///  cf.Save(filename);
        ///  
        /// </code>
        /// </example>
        public ICFStorage AddStorage(String storageName)
        {
            CheckDisposed();

            if (String.IsNullOrEmpty(storageName))
                throw new CFException("Stream name cannot be null or empty");

            // Add new Storage directory entry
            CFStorage cfo = null;

            cfo = new CFStorage(this.CompoundFile);
            cfo.DirEntry.SetEntryName(storageName);

            try
            {
                // Add object to Siblings tree
                //Trace.WriteLine("**** INSERT STORAGE " + cfo.Name + "******");
                Children.Insert(cfo);
                //Children.Print();
            }
            catch (RBTreeDuplicatedItemException)
            {

                CompoundFile.ResetDirectoryEntry(cfo.DirEntry.SID);
                cfo = null;
                throw new CFDuplicatedItemException("An entry with name '" + storageName + "' is already present in storage '" + this.Name + "' ");
            }


            //CompoundFile.RefreshIterative(Children.Root);
            this.DirEntry.Child = Children.Root.Value.DirEntry.SID;
            return cfo;
        }

        //public List<ICFObject> GetSubTreeObjects()
        //{
        //    List<ICFObject> result = new List<ICFObject>();

        //    children.VisitTree(TraversalMethod.Inorder,
        //         delegate(BinaryTreeNode<ICFObject> node)
        //         {
        //             result.Add(node.Value);
        //         });

        //    return result;
        //}

        /// <summary>
        /// Visit all entities contained in the storage applying a user provided action
        /// </summary>
        /// <exception cref="T:OpenMcdf.CFDisposedException">Raised when visiting items of a closed compound file</exception>
        /// <param name="action">User <see cref="T:OpenMcdf.VisitedEntryAction">action</see> to apply to visited entities</param>
        /// <param name="recursive"> Visiting recursion level. True means substorages are visited recursively, false indicates that only the direct children of this storage are visited</param>
        /// <example>
        /// <code>
        /// const String STORAGE_NAME = "report.xls";
        /// CompoundFile cf = new CompoundFile(STORAGE_NAME);
        ///
        /// FileStream output = new FileStream("LogEntries.txt", FileMode.Create);
        /// TextWriter tw = new StreamWriter(output);
        ///
        /// VisitedEntryAction va = delegate(CFItem item)
        /// {
        ///     tw.WriteLine(item.Name);
        /// };
        ///
        /// cf.RootStorage.VisitEntries(va, true);
        ///
        /// tw.Close();
        /// </code>
        /// </example>
        public void VisitEntries(Action<CFItem> action, bool recursive)
        {
            CheckDisposed();

            if (action != null)
            {
                List<RBNode<CFItem>> subStorages
                    = new List<RBNode<CFItem>>();

                Action<RBNode<CFItem>> internalAction =
                    delegate(RBNode<CFItem> targetNode)
                    {
                        action(targetNode.Value);

                        if (targetNode.Value.DirEntry.Child != DirectoryEntry.NOSTREAM)
                            subStorages.Add(targetNode);

                        return;
                    };

                this.Children.VisitTreeNodes(internalAction);

                if (recursive && subStorages.Count > 0)
                    foreach (RBNode<CFItem> n in subStorages)
                    {
                        ((CFStorage)n.Value).VisitEntries(action, recursive);
                    }
            }
        }


        //public void DeleteStream(String name)
        //{
        //    Delete(name, typeof(CFStream));
        //}

        //public void DeleteStorage(String name)
        //{
        //    Delete(name, typeof(CFStorage));
        //}



        /// <summary>
        /// Remove an entry from the current storage and compound file.
        /// </summary>
        /// <param name="entryName">The name of the entry in the current storage to delete</param>
        /// <example>
        /// <code>
        /// cf = new CompoundFile("A_FILE_YOU_CAN_CHANGE.cfs", UpdateMode.Update, true, false);
        /// cf.RootStorage.Delete("AStream"); // AStream item is assumed to exist.
        /// cf.Commit(true);
        /// cf.Close();
        /// </code>
        /// </example>
        /// <exception cref="T:OpenMcdf.CFDisposedException">Raised if trying to delete item from a closed compound file</exception>
        /// <exception cref="T:OpenMcdf.CFItemNotFound">Raised if item to delete is not found</exception>
        /// <exception cref="T:OpenMcdf.CFException">Raised if trying to delete root storage</exception>
        public void Delete(String entryName)
        {
            CheckDisposed();

            // Find entry to delete
            CFMock tmp = new CFMock(entryName, StgType.StgInvalid);

            CFItem foundObj = null;

            this.Children.TryLookup(tmp, out foundObj);

            if (foundObj == null)
                throw new CFItemNotFound("Entry named [" + entryName + "] was not found");

            //if (foundObj.GetType() != typeCheck)
            //    throw new CFException("Entry named [" + entryName + "] has not the correct type");

            if (foundObj.DirEntry.StgType == StgType.StgRoot)
                throw new CFException("Root storage cannot be removed");

            switch (foundObj.DirEntry.StgType)
            {
                case StgType.StgStorage:

                    CFStorage temp = (CFStorage)foundObj;

                    // This is a storage. we have to remove children items first
                    foreach (RBNode<CFItem> de in temp.Children)
                    {
                        temp.Delete(de.Value.Name);
                    }

                    // ...then we need to rethread the root of siblings tree...
                    if (this.Children.Root != null)
                        this.DirEntry.Child = this.Children.Root.Value.DirEntry.SID;
                    else
                        this.DirEntry.Child = DirectoryEntry.NOSTREAM;

                    // ...and finally Remove storage item from children tree...
                    this.Children.Delete(foundObj);

                    // ...and remove directory (storage) entry
                    this.CompoundFile.RemoveDirectoryEntry(foundObj.DirEntry.SID);

                    //Trace.WriteLine("**** DELETED STORAGE " + entryName + "******");

                    // Synchronize tree with directory entries
                    //this.CompoundFile.RefreshIterative(this.Children.Root);

                    break;

                case StgType.StgStream:

                    // Remove item from children tree
                    this.Children.Delete(foundObj);
                    //Trace.WriteLine("**** DELETED STREAM " + entryName + "******");
                    
                // Synchronize tree with directory entries
                    //this.CompoundFile.RefreshIterative(this.Children.Root);

                    // Rethread the root of siblings tree...
                    if (this.Children.Root != null)
                        this.DirEntry.Child = this.Children.Root.Value.DirEntry.SID;
                    else
                        this.DirEntry.Child = DirectoryEntry.NOSTREAM;

                    // Remove directory entry
                    this.CompoundFile.RemoveDirectoryEntry(foundObj.DirEntry.SID);

                    break;
            }

            //// Refresh recursively all SIDs (invariant for tree sorting)
            //VisitedEntryAction action = delegate(CFSItem target)
            //{
            //    if( ((IDirectoryEntry)target).SID>foundObj.SID )
            //    {
            //        ((IDirectoryEntry)target).SID--;
            //    }                   


            //    ((IDirectoryEntry)target).LeftSibling--;
            //};
        }
    }
}
