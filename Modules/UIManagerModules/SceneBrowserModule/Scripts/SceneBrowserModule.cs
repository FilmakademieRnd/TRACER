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

//! @file "SceneBrowserModule.cs"
//! @brief implementation of TRACER scene browser menu (no matter if online or locally)
//! @author Thomas Krüger
//! @version 0.1
//! @note mockup implementation
//! @date 11.11.2026

using System;
using System.Collections.Generic;


namespace tracer
{
    //!
    //! Implementation if TRACER scene browser menu
    //!
    public class SceneBrowserModule : UIManagerModule{
        
        private MenuTree m_menu;

        //!
        //! constructor
        //! @param name The name of this module.
        //! @param Manager The manager (SceneManager) of this module.
        //!
        public SceneBrowserModule(string name, Manager manager) : base(name, manager) { }

        private void Logout() {
            //simple return to the login-screen/main menu
            //right now do so by "restarting" the app?
        }

        //!
        //! Init and setup of the module and it's UI.
        //!
        protected override void Start(object sender, EventArgs e){
            base.Start(sender, e);

            //Mockup User Info
            //IMAGE/PIC TODO: implement simple class to do so
                //MenuItem userPicMenuItem = new MenuItem(MenuItem.IType.TEXT, null, );
                //ClassParameter<MenuItem> userPic = new ClassParameter<MenuItem>(userPicMenuItem, "userPic");
            ClassParameter<string> userName = new ClassParameter<string>("Ludwig Mies van der Rohe", "userName");
            ClassParameter<Action> logoutButton = new ClassParameter<Action>(Logout, "Logout");

            //Scene List
            List<string> sceneNames = new();
            List<ClassParameter<string>> sceneInfos = new();
            //scene currently being edited (round image with color)
            List<ClassParameter<Action>> sceneLoadButton = new();          
            
            //Get scenes - be careful to call this after SceneStorageModule is implemented
            GatherSceneList(sceneNames, sceneInfos, sceneLoadButton);

            m_menu = new MenuTree()
              .Begin(MenuItem.IType.VSPLIT)
                   // === Chapter 1: User Info (Pic, Name, Online, Logout-Button) ===
                   .Add("User Info")
                   .Begin(MenuItem.IType.HSPLIT)
                       .Add(userName)
                       .Add(logoutButton)
                   .End();

            // === Chapter 1: Scene List
            m_menu.Add("Scene List");
            for(int x = 0; x<5; x++) {
                m_menu.Begin(MenuItem.IType.HSPLIT)
                    .Add(MenuItem.IType.IMAGE, null, DynamicImageGenerator.GenerateAbstractLandscape32x32())
                    //.Begin(MenuItem.IType.VSPLIT)
                        .Add(sceneNames[x])
                        //.Add(sceneInfos[x])
                    //.End()
                    .Add(sceneLoadButton[x])
                .End();
            }
            m_menu.End();

            m_menu.caption = "Scene Browser";
            m_menu.iconResourceLocation = "Images/menu_open_button"; //if we want to open it seperately (maybe for testing)
            manager.addMenu(m_menu);

            // add elements to start menu (for testing it)
            ClassParameter<Action> openMenuButton = new ClassParameter<Action>(OpenSceneBrowserMenu, "Browse Scenes");
            manager.startMenu
                .Begin(MenuItem.IType.HSPLIT)
                    .Add(openMenuButton)
                .End();

        }

        //!
        //! Function to grab scene data from SceneManager
        //!
        private void GatherSceneList(List<string> _sceneNames, List<ClassParameter<string>> _sceneInfos, List<ClassParameter<Action>> _sceneLoadButton){
            List<AbstractParameter> sceneList = core.getManager<SceneManager>().GetAvailableScenesForUserToLoad();

            if(sceneList == null || sceneList.Count == 0) {
                _sceneNames.Add("Reithaus");
                _sceneNames.Add("Laufstall");
                _sceneNames.Add("Rathaus");
                _sceneNames.Add("Ruine");
                _sceneNames.Add("Neubau B2");

                _sceneInfos.Add(new ClassParameter<string>("Ort:Ludwigsburg, Created:2026-11-02, Modified:2026-11-11, Size:120mb, Version:1.4, ScanInfo:Drohne, Wetter:bewölkt & trocken, RechteInhaber:Firmen GmbH, Info:Erster Scan am Tag xyz von Person abc. Hat ungefähr 4 Stunden gedauert.", "sceneInfo"));
                _sceneInfos.Add(new ClassParameter<string>("Ort:Stuttgart, Created:2026-10-15, Modified:2026-11-05, Size:340mb, Version:1.2, ScanInfo:Terrestrischer Laserscanner, Wetter:sonnig & windstill, RechteInhaber:GeoSpatial Media GmbH, Info:Innenbereich-Scan der Haupthalle von Person dfe. Scanzeit betrug etwa 2,5 Stunden.", "sceneInfo"));
                _sceneInfos.Add(new ClassParameter<string>("Ort:Kornwestheim, Created:2026-11-04, Modified:2026-11-08, Size:85mb, Version:1.0, ScanInfo:Handheld LiDAR, Wetter:bedeckt & trocken, RechteInhaber:TwinWorks AG, Info:Fassadenerfassung Westflügel von Person ghi. Aufnahme innerhalb von 90 Minuten abgeschlossen.", "sceneInfo"));
                _sceneInfos.Add(new ClassParameter<string>("Ort:Bietigheim-Bissingen, Created:2026-09-28, Modified:2026-10-12, Size:520mb, Version:2.0, ScanInfo:Drohne & Photogrammetrie, Wetter:heiter & klar, RechteInhaber:Firmen GmbH, Info:Flächendeckender Geländescan von Person jkl. Gesamtdauer rund 5 Stunden inklusive Akkuwechsel.", "sceneInfo"));
                _sceneInfos.Add(new ClassParameter<string>("Ort:Ludwigsburg, Created:2026-11-07, Modified:2026-11-10, Size:1.1gb, Version:1.1, ScanInfo:Mobiles Mapping-System, Wetter:leicht bewölkt & trocken, RechteInhaber:InfraScan Solutions, Info:Straßenzug und Bürgersteige erfasst von Person mno. Fahrzeit lag bei circa 3 Stunden.", "sceneInfo"));

                //either has to become a dynamic call or grab the current scene data via click
                _sceneLoadButton.Add( new ClassParameter<Action>(LoadScene, "Load") );
                _sceneLoadButton.Add( new ClassParameter<Action>(LoadScene, "Load") );
                _sceneLoadButton.Add( new ClassParameter<Action>(LoadScene, "Load") );
                _sceneLoadButton.Add( new ClassParameter<Action>(LoadScene, "Load") );
                _sceneLoadButton.Add( new ClassParameter<Action>(LoadScene, "Load") );
            } else {
                /*
                // Sort by something (created date? name? )
                sceneDataList.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase));
                foreach (SceneDataStruct sceneData in sceneDataList){
                    //create data
                    //...
                }
                */
            }
        }

        private void OnSceneHovered(object sender, int selectedIndex){
            //we need a "rect" around the scene image, name and info so it can be hovered
        }
        
        //!
        //! Callback function to open this menu
        //!
        private void OpenSceneBrowserMenu()
        {
            manager.hideMenu();
            manager.showMenu(m_menu);
        }

        //!
        //! Function that determines the current scene filepath and calls the load function.
        //!
        private void LoadScene(){
            //how to call this one on the SceneManager so it will trigger the actual load via its module?
            //listerner for onLoadScene(string path) ?
        }
    }

}
