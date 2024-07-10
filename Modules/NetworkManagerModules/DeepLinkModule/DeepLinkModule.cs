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


//! @file "DeepLinkModule.cs"
//! @brief scanning a QR code using device camera will automaticaly start the app and request the scene 
//! @QR code text example: "vpetapp://ip?127.0.0.1" 
//! @author Alexandru Schwartz
//! @version 0
//! @date 08.11.2023

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace tracer
{
    public class DeepLinkModule : NetworkManagerModule
    {
 
        protected override void Init(object sender, EventArgs e)
        {
            core.updateEvent += UpdateEvent;
        }

        private void UpdateEvent(object sendre, EventArgs e)
        {
            if (!string.IsNullOrEmpty(Application.absoluteURL))
            {
                if (Application.absoluteURL.Contains("?"))
                {
                    string ip = Application.absoluteURL.Split('?')[1];
                    if (CheckIP(ip))
                    {
                        manager.ConnectUsingQrCode(ip);
                        core.updateEvent -= UpdateEvent;
                    }
                }

                if (Application.absoluteURL.Contains("#"))
                {
                    string loadScene = Application.absoluteURL.Split('#')[1];

                        core.getManager<SceneManager>().InvokeQrLoadEvent(loadScene);
                        core.updateEvent -= UpdateEvent;
                }
            }
        }
        
        private bool CheckIP(string input)
        {
            // IPv4 pattern for validation
            string ipv4Pattern =
                @"^(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)$";

            // Check if the input matches the IPv4 pattern
            return Regex.IsMatch(input, ipv4Pattern);
        }

        public DeepLinkModule(string name, Manager manager) : base(name, manager)
        {
            if (core.isServer)
                load = false;
        }

        protected override void run()
        {
            
        }
    }
}