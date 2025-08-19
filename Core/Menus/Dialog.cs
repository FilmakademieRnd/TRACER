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

//! @file "Dialog.cs"
//! @brief Implementation of the TRACER dialog, serving as internal structure to reflect dialogs.
//! @author Simon Spielmann
//! @version 0
//! @date 21.08.2022

using System;

namespace tracer
{
    public class Dialog
    {
        //!
        //! Enumeration of all suppoted dialog types.
        //!
        public enum DTypes
        {
            ERROR, WARNING, INFO, BAR
        }
        //!
        //! The actual type of the dialog.
        //!
        private DTypes m_type = DTypes.INFO;
        public DTypes type
        { get => m_type; }
        //!
        //! The actual role of the dialog.
        //!
        protected UIManager.Roles m_role;
        public UIManager.Roles role
        { get => m_role; }
        //!
        //! The caption of the dialog.
        //!
        private string m_caption = "";
        public string caption
        {   
            set 
            {
                m_caption = value;
                captionChanged?.Invoke(this, value);
            }
            get => m_caption; 
        }
        //!
        //! The message of the dialog.
        //!
        private string m_message = "";
        public string message
        { get => m_message; }
        //!
        //! The progress of the dialog.
        //!
        private int m_progress = 0;
        public int progress
        {
            get
            {
                return m_progress;
            }
            set 
            {
                m_progress = value;
                progressChanged?.Invoke(this, value);
            }
        }

        //!
        //! Event that is invoked when the dialog progress changed.
        //!
        public event EventHandler<int> progressChanged;
        //!
        //! Event that is invoked when the dialog caption changed.
        //!
        public event EventHandler<string> captionChanged;
        //!
        //! Event that is invoked when the dialog will be destroied  
        //!
        public Action destroyEvent;

        //!
        //! Constructor of the dialog class.
        //!
        //! @param caption The caption of the dialog.
        //! @param message The message of the dialog.
        //! @param type The type of the dialog.
        //! @param role The role of the dialog.
        //!
        public Dialog(string caption, string message, DTypes type, UIManager.Roles role = UIManager.Roles.EXPERT)
        {
            m_caption = caption;
            m_message = message;
            m_type = type;
            m_role = role;
        }
        //!
        //! Constructor of the dialog class.
        //!
        public Dialog(string message, DTypes type, UIManager.Roles role = UIManager.Roles.EXPERT)
        {
            if (type != DTypes.BAR)
            {
                m_caption = type.ToString();
                m_message = message;
            }
            m_type = type;
            m_role = role;
        }
        //!
        //! Constructor of the dialog class.
        //!
        public Dialog (DTypes type = DTypes.BAR, UIManager.Roles role = UIManager.Roles.EXPERT) : this("", type, role)
        {
        }

        //!
        //! Function calles, when the "close dialog" button has been pressed.
        //!
        public void Destroy()
        {
            destroyEvent?.Invoke();
        }

    }

}
