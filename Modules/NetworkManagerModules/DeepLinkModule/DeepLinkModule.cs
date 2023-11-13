/*
VPET - Virtual Production Editing Tools
tracer.research.animationsinstitut.de
https://github.com/FilmakademieRnd/VPET

Copyright (c) 2023 Filmakademie Baden-Wuerttemberg, Animationsinstitut R&D Lab

This project has been initiated in the scope of the EU funded project
Dreamspace (http://dreamspaceproject.eu/) under grant agreement no 610005 2014-2016.

Post Dreamspace the project has been further developed on behalf of the
research and development activities of Animationsinstitut.

In 2018 some features (Character Animation Interface and USD support) were
addressed in the scope of the EU funded project SAUCE (https://www.sauceproject.eu/)
under grant agreement no 780470, 2018-2022

This program is free software; you can redistribute it and/or modify it under
the terms of the MIT License as published by the Open Source Initiative.

This program is distributed in the hope that it will be useful, but WITHOUT
ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS
FOR A PARTICULAR PURPOSE. See the MIT License for more details.

You should have received a copy of the MIT License along with
this program; if not go to
https://opensource.org/licenses/MIT
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
using tracer;
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