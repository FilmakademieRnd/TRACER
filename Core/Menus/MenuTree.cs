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

//! @file "MenuTree.cs"
//! @brief Implementation of the TRACER UI Menu Tree, serving as internal structure to reflect menus.
//! @author Simon Spielmann
//! @author Jonas Trottnow
//! @version 0
//! @date 21.01.2022

using System;
using System.Collections.Generic;

namespace tracer
{
    public class MenuTree : MenuButton
    {
        //!
        //! Flag determining whether a menu is visible.
        //!
        public bool visible = false;
        //!
        //! Flag determining whether a menu is scrollable.
        //!
        public bool scrollable = false;
        //!
        //! Stack for inserting new items.
        //! 
        private Stack<MenuItem> m_stack = new Stack<MenuItem>();
        //!
        //! List of Items storing menu element data.
        //! 
        public List<MenuItem> Items { get; } = new List<MenuItem>();

        //!
        //! Constructor
        //!
        public MenuTree() : base("", null, null) { }
        public MenuTree(List<UIManager.Roles> roles) : base("", null, roles) { }

        //!
        //! Adds a new string item as branch into the menu tree.
        //! 
        //! @param text The text stored in the menue item.
        //! 
        public MenuTree Begin(string text)
        {
            return Begin(MenuItem.IType.TEXT, new Parameter<string>(text, "InfoText" + Items.Count.ToString()));
        }

        //!
        //! Adds a new branch with a given type into the menu tree.
        //! 
        //! @param type The type of the new item.
        //! 
        public MenuTree Begin(MenuItem.IType type)
        {
            return Begin(type, null);
        }

        //!
        //! Adds a new parameter item as branch into the menu tree.
        //! 
        //! @param param The parameter stored in the menue item.
        //! 
        public MenuTree Begin(AbstractParameter param)
        {
            return Begin(MenuItem.IType.PARAMETER, param);
        }

        //!
        //! Adds a new parameter item with a given type as branch into the menu tree.
        //! 
        //! @param type The type of the new item.
        //! @param param The parameter stored in the menue item.
        //! 
        private MenuTree Begin(MenuItem.IType type, AbstractParameter param = null)
        {
            if (m_stack.Count == 0)
            {
                MenuItem item = new MenuItem(type, param, null);
                Items.Add(item);
                m_stack.Push(item);
            }
            else
            {
                MenuItem item = m_stack.Peek().Add(type, param);
                m_stack.Push(item);
            }

            return this;
        }

        //!
        //! Adds a new item with a given type into the current branch.
        //! 
        //! @param type The type of the new item.
        //! 
        public MenuTree Add(MenuItem.IType type)
        {
            return Add(type, null);
        }

        //!
        //! Adds a new string item into the current branch.
        //! 
        //! @param text The text stored in the menue item.
        //! 
        public MenuTree Add(string text)
        {
            return Add(MenuItem.IType.TEXT, new Parameter<string>(text, "InfoText" + Items.Count.ToString()));
        }

        public MenuTree Add(string text, bool isTextSection = false)
        {
            if (isTextSection)
                return Add(MenuItem.IType.TEXTSECTION, new Parameter<string>(text, "Text Section" + Items.Count.ToString()));
            else
                return Add(MenuItem.IType.TEXT, new Parameter<string>(text, "InfoText" + Items.Count.ToString()));
        }
        //!
        //! Adds a new parameter item into the current branch.
        //! 
        //! @param param The parameter stored in the menue item.
        //! 
        public MenuTree Add(AbstractParameter param)
        {
            return Add(MenuItem.IType.PARAMETER, param);
        }

        //!
        //! Adds a new parameter item with a given type to the current branch.
        //! 
        //! @param type The type of the new item.
        //! @param param The parameter stored in the menue item.
        //! 
        private MenuTree Add(MenuItem.IType type, AbstractParameter param = null)
        {
            m_stack.Peek().Add(type, param);
            return this;
        }

        //!
        //! Finalizes the current branch in the menu tree.
        //!
        public MenuTree End()
        {
            m_stack.Pop();
            return this;
        }
    }

    //!
    //! Class defining menuTree items, storing the menus data.
    //!
    public class MenuItem
    {
        //!
        //! Enumeration of all suppoted menuItem types.
        //!
        public enum IType
        {
            HSPLIT, VSPLIT, SPACE, TEXT, TEXTSECTION, PARAMETER
        }
        //!
        //! The type of the menu item.
        //!
        public IType Type { get; }
        //!
        //! The parameter stored in the menu item.
        //!
        public AbstractParameter Parameter { get; }
        //!
        //! The _parent of the menu item.
        //!
        public MenuItem Parent { get; }
        //!
        //! The list of the chlildren of the menu item.
        //!
        public List<MenuItem> Children { get; }
        //!
        //! The constructor of the menuItem
        //!
        //! @param type The type of the menuItem.
        //! @param param The parameterm the menuItem is associated with.
        //! @param parant The _parent of the menuItem.
        //!
        public MenuItem(IType type, AbstractParameter param, MenuItem parent)
        {
            Type = type;
            Parameter = param;
            Parent = parent;
            Children = new List<MenuItem>();
        }
        //!
        //! Adds a new item with a given type and the corresponding parameter to the items child list.
        //! 
        //! @param type The type of the new item.
        //! @param param The parameter stored in the menue item.
        //! 
        public MenuItem Add(IType type, AbstractParameter param)
        {
            MenuItem item = new MenuItem(type, param, this);
            Children.Add(item);
            return item;
        }
    }

}
