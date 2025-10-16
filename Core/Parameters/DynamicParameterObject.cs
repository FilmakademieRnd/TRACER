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

//! @file "DynamicParameterObject.cs"
//! @brief implementation Dynamic Parameter Object
//! @author Alexandru Schwartz
//! @author Simon Hagg
//! @author Simon Spielmann
//! @version 0
//! @date 03.08.2022

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using tracer;
using UnityEngine;

public class DynamicParameterObject : ParameterObject
{
    //!
    //! Factory to create a new ParameterObject and do it's initialisation.
    //! Use this function instead GameObject.AddComponen<>!
    //!
    //! @param gameObject The gameObject the new ParameterObject will be attached to.
    //! @sceneID The scene ID for the new ParameterObject.
    //!
    public new static DynamicParameterObject Attach(GameObject gameObject, byte sceneID = 254)
    {
        DynamicParameterObject obj = gameObject.AddComponent<DynamicParameterObject>();
        obj.Init(sceneID);

        return obj;
    }

    public void AddParameter(AbstractParameter parameter, bool subscribe = false)
    {
        parameter._id = (short) parameterList.Count;
        parameterList.Add(parameter);

        if (subscribe) 
            SubscribeToParameterChange(parameter);
    }

    public void RemoveParameters()
    {
        foreach (AbstractParameter parameter in parameterList)
            UnsubscribeFromParameterChange(parameter);

        parameterList.Clear();
    }

    //!
    //! Function to subscribe to the HasChanged Parameter Event
    ///
    /// @param parameter The parameter whose HasChanged event will be subscribed to
    ///
    public void SubscribeToParameterChange(AbstractParameter parameter)
    {
        var parameterType = parameter.GetType();
        var eventType = parameterType.GetEvent("hasChanged");
        
        if (eventType != null)
        {
            var handlerType = eventType.EventHandlerType;
            var handler = CreateHandler(parameter, handlerType);
            eventType.AddEventHandler(parameter, handler);
        }
    }

    //!
    //! Function to unsubscribe to the HasChanged Parameter Event
    ///
    /// @param parameter The parameter whose HasChanged event will be unsubscribed
    ///
    public void UnsubscribeFromParameterChange(AbstractParameter parameter)
    {
        var parameterType = parameter.GetType();
        var eventType = parameterType.GetEvent("hasChanged");

        if (eventType != null && parameterType.GetGenericArguments().Length > 0)
        {
            var handlerType = eventType.EventHandlerType;
            var handler = CreateHandler(parameter, handlerType);
            eventType.RemoveEventHandler(parameter, handler);
        }
    }


    ///
    /// Creates a delegate for handling the HasChanged event
    ///
    /// @param parameter The parameter whose HasChanged event handler is being created
    /// @param handlerType The type of the event handler delegate
    /// @return A delegate for the event handler method
    ///
    private Delegate CreateHandler(AbstractParameter parameter, Type handlerType)
    {
        var type = GetType();
        var methodInfo = type.GetMethod("UpdateDynamicParameter");
        //var methodInfo = this.GetType().GetMethod(nameof(UpdateDynamicParameter), BindingFlags.NonPublic | BindingFlags.Instance);
        var eventType = parameter.GetType().GetGenericArguments()[0]; // Get the generic type argument T
        var genericMethod = methodInfo.MakeGenericMethod(eventType);
        return Delegate.CreateDelegate(handlerType, this, genericMethod);
    }

    ///
    /// Method to handle updates to a dynamic parameter.
    /// This method is called when a parameter's value changes.
    ///
    /// @typeparam T The type of the parameter's value
    /// @param sender The object that triggered the parameter update
    /// @param value The new value of the parameter
    ///
    public void UpdateDynamicParameter<T>(object sender, T value)
    {
        emitHasChanged((AbstractParameter)sender);
    }

    public delegate void noneDelegate();

    public static void Empty()
    {
        // Empty action for action parameters
        // ...could be replaces by custom code
    }
    
}
