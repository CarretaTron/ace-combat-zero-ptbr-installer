using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Ac0PtBrInstaller
{
    /// <summary>Selects a filesystem directory with the Explorer folder dialog.</summary>
    public static class FolderPicker
    {
        public static string PickFolder(IWin32Window owner, string currentPath)
        {
            if (Environment.OSVersion.Version.Major >= 6)
            {
                try
                {
                    return PickWithExplorer(owner, currentPath);
                }
                catch (COMException) { }
                catch (DllNotFoundException) { }
                catch (EntryPointNotFoundException) { }
                catch (PlatformNotSupportedException) { }
                catch (InvalidCastException) { }
            }

            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Selecione a pasta ACZero que contém acz.exe, a.arc e d.arc.";
                dialog.ShowNewFolderButton = false;
                dialog.RootFolder = Environment.SpecialFolder.MyComputer;
                if (!String.IsNullOrWhiteSpace(currentPath) && Directory.Exists(currentPath))
                    dialog.SelectedPath = currentPath;
                return dialog.ShowDialog(owner) == DialogResult.OK ? dialog.SelectedPath : null;
            }
        }

        private static string PickWithExplorer(IWin32Window owner, string currentPath)
        {
            IFileOpenDialog dialog = null;
            IShellItem initialFolder = null;
            IShellItem result = null;
            IntPtr name = IntPtr.Zero;
            try
            {
                dialog = (IFileOpenDialog)new FileOpenDialogCom();
                uint options;
                dialog.GetOptions(out options);
                // PICKFOLDERS, FORCEFILESYSTEM, NOCHANGEDIR, PATHMUSTEXIST, DONTADDTORECENT.
                dialog.SetOptions(options | 0x20u | 0x40u | 0x08u | 0x800u | 0x02000000u);
                dialog.SetTitle("Selecione a pasta do Ace Combat Zero");
                dialog.SetOkButtonLabel("Selecionar pasta");
                if (!String.IsNullOrWhiteSpace(currentPath) && Directory.Exists(currentPath))
                {
                    Guid shellItemId = typeof(IShellItem).GUID;
                    int createResult = SHCreateItemFromParsingName(
                        Path.GetFullPath(currentPath), IntPtr.Zero, ref shellItemId, out initialFolder);
                    if (createResult >= 0 && initialFolder != null)
                        dialog.SetFolder(initialFolder);
                }

                int showResult = dialog.Show(owner == null ? IntPtr.Zero : owner.Handle);
                if (showResult == unchecked((int)0x800704C7))
                    return null; // The user cancelled; do not open another dialog.
                Marshal.ThrowExceptionForHR(showResult);
                dialog.GetResult(out result);
                result.GetDisplayName(0x80058000u, out name); // SIGDN_FILESYSPATH.
                return Marshal.PtrToStringUni(name);
            }
            finally
            {
                if (name != IntPtr.Zero) Marshal.FreeCoTaskMem(name);
                Release(result);
                Release(initialFolder);
                Release(dialog);
            }
        }

        private static void Release(object value)
        {
            if (value != null && Marshal.IsComObject(value))
                Marshal.ReleaseComObject(value);
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
        private static extern int SHCreateItemFromParsingName(
            string path, IntPtr bindContext, ref Guid interfaceId,
            [MarshalAs(UnmanagedType.Interface)] out IShellItem shellItem);

        [ComImport]
        [Guid("DC1C5A9C-E88A-4DDE-A5A1-60F82A20AEF7")]
        private class FileOpenDialogCom { }

        // Keep the full native method order: the unused methods are COM vtable slots.
        [ComImport]
        [Guid("D57C7288-D4AD-4768-BE02-9D969532D960")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IFileOpenDialog
        {
            [PreserveSig] int Show(IntPtr owner);
            void SetFileTypes(uint count, IntPtr filterSpecifications);
            void SetFileTypeIndex(uint index);
            void GetFileTypeIndex(out uint index);
            void Advise(IntPtr events, out uint cookie);
            void Unadvise(uint cookie);
            void SetOptions(uint options);
            void GetOptions(out uint options);
            void SetDefaultFolder(IShellItem folder);
            void SetFolder(IShellItem folder);
            void GetFolder(out IShellItem folder);
            void GetCurrentSelection(out IShellItem selection);
            void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string name);
            void GetFileName(out IntPtr name);
            void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string title);
            void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string label);
            void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string label);
            void GetResult(out IShellItem result);
            void AddPlace(IShellItem place, uint placement);
            void SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)] string extension);
            void Close(int result);
            void SetClientGuid(ref Guid guid);
            void ClearClientData();
            void SetFilter(IntPtr filter);
            void GetResults(out IntPtr results);
            void GetSelectedItems(out IntPtr selections);
        }

        [ComImport]
        [Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellItem
        {
            void BindToHandler(IntPtr bindContext, ref Guid handlerId,
                ref Guid interfaceId, out IntPtr result);
            void GetParent(out IShellItem parent);
            void GetDisplayName(uint displayNameType, out IntPtr name);
            void GetAttributes(uint mask, out uint attributes);
            void Compare(IShellItem other, uint hint, out int order);
        }
    }
}
