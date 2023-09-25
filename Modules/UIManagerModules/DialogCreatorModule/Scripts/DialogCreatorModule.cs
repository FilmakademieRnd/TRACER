/*
TRACER FOUNDATION - 
Toolset for Realtime Animation, Collaboration & Extended Reality
tracer.research.animationsinstitut.de
https://github.com/FilmakademieRnd/TRACER

Copyright (c) 2023 Filmakademie Baden-Wuerttemberg, Animationsinstitut R&D Lab

TRACER is a development by Filmakademie Baden-Wuerttemberg, Animationsinstitut
R&D Labs in the scope of the EU funded project MAX-R (101070072) and funding on
the own behalf of Filmakademie Baden-Wuerttemberg.  Former EU projects Dreamspace
(610005) and SAUCE (780470) have inspired the TRACER development.

This program is distributed in the hope that it will be useful, but WITHOUT
ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS
FOR A PARTICULAR PURPOSE. See the MIT License for more details.
You should have received a copy of the MIT License along with this program; 
if not go to https://opensource.org/licenses/MIT
*/

//! @file "DialogCreatorModule.cs"
//! @brief Implementation of the DialogCreatorModule, creating UI dialogs based on a Dialog object.
//! @author Simon Spielmann
//! @version 0
//! @date 10.08.2022

using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace tracer
{
    public class DialogCreatorModule : UIManagerModule
    {
        //!
        //! A reference to the dialog prefab.
        //!
        private GameObject m_canvasPrefab;
        //!
        //! A reference the the dialog prefab instance.
        //!
        private GameObject m_canvas;
        //!
        //! A reference to the dialogs slider element.
        //!
        private Slider m_slider = null;
        //!
        //! A reference to the dialogs caption.
        //!
        private TextMeshProUGUI m_captionText = null;
        //!
        //! The last shown dialog.
        //!
        private Dialog m_oldDialog;
        //!
        //! Constructor
        //! @param name Name of this module
        //! @param core Reference to the TRACER core
        //!
        public DialogCreatorModule(string name, Manager manager) : base(name, manager)
        {
        }


        //!
        //! Init Function.
        //!
        protected override void Init(object sender, EventArgs e)
        {
            manager.dialogRequested += createDialog;
            m_canvasPrefab = Resources.Load("Prefabs/DialogCanvas") as GameObject;
            m_canvasPrefab.GetComponent<Canvas>().sortingOrder = 20;

            //[EXAMPLE]
            // >>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>
            //manager.showDialog(new Dialog("Das ist ein Test! ;)", Dialog.DTypes.WARNING, UIManager.Roles.SCOUT));
            // <<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<
        }

        //!
        //! function called before Unity destroys the TRACER core.
        //! 
        //! @param sender A reference to the TRACER core.
        //! @param e Arguments for these event. 
        //! 
        //!
        protected override void Cleanup(object sender, EventArgs e)
        {
            base.Cleanup(sender, e);
            manager.dialogRequested -= createDialog;
        }

        //!
        //! Function creating a dialog UI element based on a Dialog object.
        //!
        //! @param sender A reference to the UI manager.
        //! @param menu A reference to the Dialog object used to create the UI elements of a dialog.
        //!
        void createDialog(object sender, Dialog dialog)
        {
            destroyDialog(this, EventArgs.Empty);

            if (dialog == null ||
                (manager.activeRole != dialog.role && dialog.role != UIManager.Roles.EXPERT ))
                return;

            m_oldDialog = dialog;
            m_canvas = GameObject.Instantiate(m_canvasPrefab);

            GameObject caption = m_canvas.transform.FindDeepChild("Caption").gameObject;
            GameObject message = m_canvas.transform.FindDeepChild("Message").gameObject;
            GameObject progressBar = m_canvas.transform.FindDeepChild("ProgressBar").gameObject;
            GameObject button = m_canvas.transform.FindDeepChild("Button").gameObject;

            m_captionText = caption.GetComponent<TextMeshProUGUI>();
            m_captionText.text = dialog.caption;
            button.GetComponent<Button>().onClick.AddListener(() => destroyDialog(this, EventArgs.Empty));

            switch (dialog.type)
            {
                case Dialog.DTypes.BAR:
                    progressBar.SetActive(true);
                    message.SetActive(false);
                    m_slider = progressBar.GetComponent<Slider>();
                    dialog.progressChanged += changeDialogProgress;
                    dialog.captionChanged += changeDialogCaption;
                    break;
                default:
                    message.GetComponent<TextMeshProUGUI>().text = dialog.message;
                    break;
            }
        }

        //!
        //! Function that is called if the dialogs progess change.
        //!
        //! @param sender A reference to the dialog object.
        //! @param prograss the new progress to be shown.
        //!
        private void changeDialogProgress(object sender, int progress)
        {
            if (m_slider)
                m_slider.value = progress;

        }

        //!
        //! Function that is called if the dialogs caption change.
        //!
        //! @param sender A reference to the dialog object.
        //! @param caption the new caption to be shown.
        //!
        private void changeDialogCaption(object sender, string caption)
        {
            if (m_captionText)
                m_captionText.text = caption;
        }

        //!
        //! Function to destroy all created UI elements of a dialog.
        //!
        private void destroyDialog(object sender, EventArgs e)
        {
            m_slider = null;
            m_captionText = null;

            if (m_oldDialog != null)
                m_oldDialog.progressChanged -= changeDialogProgress;
            if (m_oldDialog != null)
                m_oldDialog.captionChanged -= changeDialogCaption;
         
            UnityEngine.Object.DestroyImmediate(m_canvas);
        }
    }
}
