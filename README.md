# TRACER FOUNDATION - Toolset for Realtime Animation, Collaboration & Extended Reality


## Description

TRACER is a software agnostic communication infrastructure and toolset for plugging open-source tools into a production pipeline, establishing interoperability between open source and proprietary tools, targeting real-time collaboration and XR productions, with an operational layer for exchanging data objects and updates including animation and scene data, synchronization of scene updates of different client applications (Blender, UE, Unity, VPET ...), parameter harmonization between different engines/renderers, unified scene distribution and scene export which stores the current state of the scene. Furthermore, it can act as an Animation Host, to support XR and Virtual Production high demand to be able to use, exchange and direct animations in real-time environments. TRACER can be integrated and interact with any GameEngine (e.g. Unreal) or DCC (e.g. Blender, Maya, ...) through the provided open APIs and protocols.

![TRACER](.doc/img/tracer_info_graphics_shematic.png)

**TRACER web site:** [animationsinstitut.de/de/forschung/tools/tracer](https://animationsinstitut.de/de/forschung/tools/tracer)


## Repository Content

TRACER FOUNDATION features:

- An open transfer protocol to provide 3D scenes coming from any DCC, game engine or XR application
- Time-synchronized scene updates for real-time collaboration and multi-user applications
- Generalized parameter representation and synchronization
- Network communication through netMQ/zeroMQ
- AR support (incl. Tracking and Markers)
- Touch UI (Buttons and Navigation Gestures)
- Modular, extendible, module-based architecture
And much more...


The TRACER Foundation itself is developed in C#, thereby it is well suited to target any Desktop, VR or XR platform.


## Additional resources

* [TRACER Web Site](https://animationsinstitut.de/en/research/tools/tracer)


## About

![Animationsinstitut R&D](.doc/img/logo_rnd.jpg)

TRACER is a development by Filmakademie Baden-Wuerttemberg, Animationsinstitut R&D Labs in the scope of the EU funded project MAX-R (101070072) and funding on the own behalf of Filmakademie Baden-Wuerttemberg.  Former EU projects Dreamspace (610005) and SAUCE (780470) have inspired the TRACER development.

## Funding

![Animationsinstitut R&D](.doc/img/EN_FundedbytheEU_RGB_POS_rs.png)

This project has received funding from the European Union's Horizon Europe Research and Innovation Programme under Grant Agreement No 101070072 MAX-R.
This project has received funding from the European Union’s Horizon 2020 Research and Innovation Programme under Grant Agreement No 780470 SAUCE.
This research has received funding from the European Commission’s Seventh Framework Programme under grant agreement no 610005 DREAMSPACE.


## License
TRACER is a open-sorce development by Filmakademie Baden-Wuerttemberg's Animationsinstitut.  
The framework is licensed under MIT. See [License file](LICENSE.TXT) for more details.
