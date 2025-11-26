/*
-----------------------------------------------------------------------------------
TRACER FOUNDATION -
Toolset for Realtime Animation, Collaboration & Extended Reality

Copyright (c) 2024 Filmakademie Baden-Wuerttemberg, Animationsinstitut R&D Labs
https://research.animationsinstitut.de/tracer
https://github.com/FilmakademieRnd/TRACER

TRACER FOUNDATION is a development by Filmakademie Baden-Wuerttemberg,
Animationsinstitut R&D Labs in the scope of the EU funded project
MAX-R (101070072) and funding on the own behalf of Filmakademie Baden-Wuerttemberg.
Former EU projects Dreamspace (610005) and SAUCE (780470) have inspired the
TRACER FOUNDATION development.

This program is distributed in the hope that it will be useful, but WITHOUT
ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS
FOR A PARTICULAR PURPOSE. See the MIT License for more details.
You should have received a copy of the MIT License along with this program;
if not go to https://opensource.org/licenses/MIT
-----------------------------------------------------------------------------------
*/

//! @file "NativeFilePicker.cs"
//! @brief Cross-platform native file picker wrapper
//! @author Jonas Trottnow
//! @version 1.0
//! @date 2025-11-04

using System;
using System.Runtime.InteropServices;
using AOT;
using UnityEngine;

namespace tracer
{
    /// <summary>
    /// Cross-platform native file picker for iOS and other platforms
    /// </summary>
    public static class NativeFilePicker
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void FilePickerCallback(string path);

        private static FilePickerCallback s_callback;

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void _ShowIOSFilePicker(string fileTypes, FilePickerCallback callback);
#endif

#if UNITY_IOS && !UNITY_EDITOR
        [MonoPInvokeCallback(typeof(FilePickerCallback))]
        private static void OnFilePickedCallback(string path)
        {
            if (s_callback != null)
            {
                s_callback(path);
                s_callback = null;
            }
        }
#endif

        /// <summary>
        /// Shows a native file picker for the given file types
        /// </summary>
        /// <param name="fileTypes">Comma-separated list of file extensions (e.g., "gltf,glb")</param>
        /// <param name="callback">Callback function that receives the selected file path (empty string if cancelled)</param>
        public static void PickFile(string fileTypes, FilePickerCallback callback)
        {
#if UNITY_IOS && !UNITY_EDITOR
            s_callback = callback;
            _ShowIOSFilePicker(fileTypes, OnFilePickedCallback);
#elif UNITY_ANDROID && !UNITY_EDITOR
            // Android implementation would go here
            callback?.Invoke("");
#else
            // Editor or other platforms
            callback?.Invoke("");
#endif
        }

        /// <summary>
        /// Check if native file picker is available on current platform
        /// </summary>
        public static bool IsAvailable()
        {
#if UNITY_IOS && !UNITY_EDITOR
            return true;
#elif UNITY_ANDROID && !UNITY_EDITOR
            return false; // Not yet implemented
#else
            return false;
#endif
        }
    }
}
