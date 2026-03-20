# Philosophy 
<img src="/.doc/img/TRACERStructure.png" width="500">

The idea behind TRACER is to create an open, extensible interface that is independent of applications and programming languages, in order to transfer data sets and edit them in real time both within and between applications.
The transfer to or from any number of other instances should be possible.For historical reasons, the focus has been and continues to be on 3D applications, which is why the existing implementations are most advanced in this area. Currently, there are implementations for Blender, Unreal, Katana, and Unity. The last one is by far the most comprehensive and also the reference implementation.

# DataHub

The DataHub is a part of the TRACER network infrastructure, but a separate application that acts as a message distributor between clients, but also functions as a “cloud”-like server that provides or stores scenes. It also keeps track of all registered clients, assigns them a unique ID, and notifies other clients as soon as their connection status changes. A client can communicate directly with the DataHub via the Command Message Protocol to, for example, start a scene distribution process or negotiate a new ID. The Datahub has a modular architecture, it consists of a fixed core and plugins that implement the individual functions. [0MQ](https://zeromq.org/) is used here for network communication. 

## Ports and Sockets

TRACER uses [0MQ](https://zeromq.org/) (ZeroMQ) for network communication. Depending on the use case, different patterns are employed. These are provided in 0MQ via various socket types. The Request and Reply pattern is used for sending and receiving scenes, as well as for the Command Manager. Updates are sent and received via Publisher/Subscriber sockets, which run through an external server (DataHub) to distribute messages to any number of clients. Each client registers with its Publisher (UpdateSender) at the DataHub, and similarly, the client’s Subscriber (UpdateReceiver) registers with its corresponding Subscriber at the DataHub. A distinction is also made between whether TCP/IP sockets or WebSockets are used. TCP/IP is used for communication from an application, while WebSockets must be used for a web app or browser implementation. Accordingly, the ports used differ as listed below. 

### TCP/IP

**CommandHandler**: 5558 \
**UpdateSender**: 5557 \
**UpdateReceiver**: 5556 \
**SceneSender/Receiver**: 5555 

### WebSocket

**CommandHandler**: 5508 \
**UpdateSender**: 5507 \
**UpdateReceiver**: 5506 \
**SceneSender/Receiver**: 5505

# TRACER Implementation

TRACER has a modular structure but is initialized from a central core. All included functionality is categorized by managers. The implementation of the functionalities is carried out by modules associated with these managers.In other words, apart from core and management functionality, everything else is implemented via modules. TRACER, with its core, managers, and their modules, “lives” independently or within a host application such as Unreal, Blender, Maya, Unity, etc. 
Modules should be interchangeable at any time and therefore must not have any direct dependencies on each other. Communication between modules and the system takes place via subscription to events; communication between modules is only permitted via managers. 

## Core

The core is the starting point of a TRACER client.
It initializes managers, update events, and global settings. It is also the element that ensures that all cleanup and destruction events are triggered. 

## Managers

Managers categorize the various functionalities, thereby creating the basic structure for TRACER. They also manage the respective modules and enable communication between them .Every manager has a Settings class as a member. The values of a parameter defined within this Settings class are automatically saved when the host application is closed and reloaded the next time it starts.  
The existing Categories can be expanded depending on the required functionality. Currently, the following categories exist:\
**Input**: Summarizes user-generated physical data that flows into a client. E.g., tracking, touch, controller, etc.\
**Animation**: Animation of parameters, keys, timeline.\
**Network**: Data exchange via network, synchronization of a scene, but also individual parameters and server commands.\
**Scene**: Defines and manages a scene structure. Scenes can be classic 3D scenes with scene graphs, geometry, materials, etc., but also menu and logic functionalities and their interaction with the host engine (see ParameterObject!).\
**UI**: Creates and manages user interfaces and their interaction with the system.

## Modules

Independent instances of a module class that must be assigned to a single manager. The assignment is done via inheritance. Every non-structural extension of TRACER is done via a module. External libraries are also only integrated within modules. This allows for the encapsulation and exchange of functionality without rendering the rest of the framework inoperable. 

# Other important concepts
## Parameters
<img src="/.doc/img/ParameterDataTypes.png" width="300">
Parameters provide typed container classes that encapsulate any data type. They enable data exchange with event signaling within TRACER. They also implement the serialization and deserialization of their data. 

## Parameter Object
Concept for structuring parameters and connect TRACER with arbitrary Host Applications. It combines parameters and makes them available for more complex logic, which is to be implemented within the ParameterObject. It also establishes the connection to the host application and ensures that values and parameters of the host application are tracked and modified if necessary. This is achieved through inheritance or component patterns (depending on the host application).
When creating an instance (inheritance) or adding to a host application object (component), the newly created parameter object is automatically added to a database, which is accessible at any time via the SceneManager. Each newly created parameter object is assigned a unique ID, which allows it to be identified at any time. 
A module must also implement monitoring of its parameters so that it sends out an event as soon as one of its own parameters changes. This enables parameters to be monitored. This concept was introduced in order to bundle chaotic parameter changes in an object-oriented manner, thereby making them clearer and more efficient. 
—> Used, for example, to update TRACER Client internal components, network communication, and trigger system events.

## Parameter Update Message
<img src="/.doc/img/UpdateMessage+Key.png" height="250">

These messages are used to transfer parameter updates via 0MQ over network. Messages are sent at most once per engine update tick. However, any number of parameter updates can be included in such a message. A message consists of a header and a list of all parameter updates within the last engine tick. 

### Message Header
**ClienID \<byte\>**: The unique ID of the local Tracer instance, given by the DataHub. Fallback: if no DataHub is reachable, the last Tuple of the lacal IP is used.

**Time \<byte\>**: The current time in frames (only for sync). Will be increased depending on the framerate within engines update intervall and reseted if counter exceeds 255, but also needs to be a multiple of the current local framerate.  

**MessageType \<byte\>**: Enumeration determines the type of a message.

```c#
enum MessageType : byte{
 0 PARAMETERUPDATE,
 1 LOCK,
 2 SYNC,
 3 RESENDUPDATE,
 4 UNDOREDOADD,
 5 RESETOBJECT,
 6 DATAHUB,
 7 RPC }
```

### Parameter Update
**SceneID \<byte\>**: ID determine to which scene (or server) a SceneObject belongs. 255 is the default scene ID. ParameterObjects or SceneObjects only existing locally are assigned the scene ID 255. Objects that are created locally during runtime and are intended to interact with the network will have the local client ID as their SceneID. Objects received via network will get the sender ID as SceneID.

**SceneObjectID \<short\>**: Unique ID to identify a SceneObject in a scene. This ID will be created automaticly when a SceneObject is created or attached to a GameObject.

**ParameterID \<short\>**: Unique ID to identify a parameter within it‘s SceneObject. This ID will be created automaticly when a parameter is added to a SceneObject. Parameters living outside a SceneObject or ParameterObject will not be tracked by the system and therefore cannot trigger any event.

**ParameterType \<byte\>**: Enumeration determines the type of a message.

```c#
enum ParameterType : byte{
 0 NONE,
 1 ACTION,
 2 BOOL,
 3 INT,
 4 FLOAT,
 5 VECTOR2,
 6 VECTOR3,
 7 VECTOR4,
 8 QUATERNION,
 9 COLOR,
 10 STRING,
 11 LIST,
 100 UNKNOWN }
```

**ParameterLength \<int\>**: The length of the parameters data/payload in bytes.

**ParameterData \<T\>**: The parameters data as a byte stream. The serialization and deserialization is part implementation of the respective type of a parameter.

**AnimationData List\<Key\>** (optional): If a parameter is animated, it also contains a list of Key‘s containing the animation data. All supported parameter types can be animated. The user has to implement meaningful interploations. A base set for numeric interpolations already exists.Until now for every update in the list of key‘s the whole list will be deserialized and if applicable, also send via network!

### Key
**KeyType \<byte\>**: Enumeration determines the interplolation of a key.

```c#
enum KeyType : byte{
 0 STEP, 
 1 LINEAR, 
 2 BEZIER }
```

**KeyTime \<float\>**: The time value of a key.

**KeyTangentTime1 \<float\>**: The left tangent time value of a key.

K**eyTangentTime2 \<float\>**: The right tangent time value of a key.

**KeyValue \<T\>**: A value with the same type of the key‘s parent parameter value as a byte stream.

**KeyTangentValue1 \<T\>**: The left tangent value, with the same type of the key‘s parent parameter value as a byte stream. 

**KeyTangentValue2 \<T\>**: The right tangent value, with the same type of the key‘s parent parameter value as a byte stream.

## Command Message
Command messages are implemented via a separate, independent message port and are used to communicate with the DataHub. The messages are modeled similar to an RPC with parameter return and can be generated asynchronously by the user via the network manager. When a command message is sent, the sender waits for a response from the DataHub for a predetermined period of time. If this time limit is exceeded, the request is discarded. 
CommandMessages are currently used to **measure the quality of the network connection** to the DataHub, to **obtain a unique ID**, to **trigger the sending and receiving of scenes** to and from the DataHub, and to **retrieve information about the scenes** available on the DataHub.

<img src="/.doc/img/CommandMessage.png" height="150">

**ClienID \<byte\>**: The unique ID of the local Tracer instance, given by the DataHub. Fallback: if no DataHub is reachable, the last Tuple of the lacal IP is used.

**Time \<byte\>**: The current time in frames (only for sync). Will be increased depending on the framerate within engines update intervall and reseted if counter exceeds 255, but also needs to be a multiple of the current local framerate.

**MessageType \<byte\>**: Enumeration determines the type of a message.

```c#
enum DataHubMessageType
{
    CONNECTIONSTATUS, ID, 
	PING,
    SENDSCENE, REQUESTSCENE, FILEINFO,
	UNKNOWN = 255
}
```      
**Command <byte[]>**: The command for the DataHub to be send as a byte stream.

## Scene Description

The TRACER scene description is a proprietary binary format that allows storing 3D scenes and general parameter structures. It is divided into packages that categorize the scene by **header** with scene specific information, **scene graph** with object types, **geometry**, **characters**, **materials**, **textures**, and generalized **parameter objects**. Based on this structure, 3D data is created, stored, and transmitted. By dividing the data into packages, it is relatively easy to version a scene, by saving versions of the scene graph (NodePackage). 

### Header
Package containing global scene settings.

<img src="/.doc/img/Header.png" height="150">

**lightIntensityFactor\<float\>**: Global factor for light intensity scaling. 

**senderID\<byte\>**: The client ID from the scene sender, used as SceneObject's sceneID for received objects.

**frameRate\<byte\>**: The current framerate in fps.

### Node Package 

The Node Package defines the scne graph of a scene. It contains the tree structure as well as the types, properties, and references of the respective nodes. While the properties and references are fields of the nodes, the types and extensions are realized through inheritance.  

<img src="/.doc/img/SceneNodes.png" width="350">

**editable \<bool\>**: Flag that determines whether a node is editable or not.

**childCount \<int\>**: The number of children of the node, used to create the node tree structure.

**position \<float3\>**: Position of the node in local space.

**scale \<float3\>**: Lossy scale of the node.

**rotation \<flost4\>**: Rotation of the node in local space.

**name <byte[64]>**: Name of the node.

**geoID \<int\>**: ID for referencing the associated geometry data.

**materialID \<int\>**: The ID for referencing the associated material data.

**color \<float4\>**: The color if the node has no material assigned.

**bindPoseLength \<int\>**: Length of the array storing the bind poses.

**characterRootID \<int\>**: ID for referencing the associated character root.

**bounds <float[3]>**: The bounds of the skinned mesh in world space.

**center <float[3]>**: The center of the skinned mesh in world space.

**bindPoses <float[]>**: Bind poses of the skinned mesh stored as 4x4 matrices

**boneIDs <int[]>**: IDs for referencing the associated skeleton bones.

### Object Package

An object package contains all geometry data for an object. This data is referenced by geometry nodes in the scene graph. If the geometry is skinned, bone weights and bone indices are stored in addition to vertex positions, indices, normals, and texture coordinates. For legacy reasons, geometry can also be stored in Unity Mesh format.  

<img src="/.doc/img/ObjectPkg.png" height="150">

**vSize \<int\>**: The number of vertices in one object package.

**iSize \<int\>**: The number of indices in one object package.

**nSize \<int\>**: The number of normals in one object package.

**uvSize \<int\>**: The number of UV‘s in one object package.

**bWSize \<int\>**: The number of blend weights and bone indices in one object package.

**mesh <Mesh (Unity type)>**: Optional, a mesh stord in the Unity format. Can be empty.

**vertices <float[]>**: An array of float storing the vertex positions as x,y,z coordinates in local space.

**indices <int[]>**: An array of int storing the indices.

**normals <float[]>**: An array of float storing the normals as x,y,z in local sapce.

**uvs <float[]>**: An array of float storing the UVs as x,y coordinates.

**boneWeights <float[]>**: An array of float storing the bone weights as x,y,z,w per vertex.

**boneIndices <int[]>**: An array of int storing the bone indices.

### Character Package

A Character Package contains all ata to create a sceleton and bind it‘s corresponding scene node, including the referenced geomety to it.

<img src="/.doc/img/CharacterPkg.png" height="150">

**bMSize \<int\>**: The size of the bone mapping array.

**sSize \<int\>**: The size of the skeleton array.

**characterRootId \<int\>**: The object ID of the root of the character in the scenegraph.

**boneMapping <int[]>**: The array of IDs for referencing the associated bone objects.

**skeletonMapping <int[]>**: The array of IDs for referencing the sceleton objects.

**bonePosition <float[]>**: The array of bone positions for this character as vec3[].

**boneRotation <float[]>**: The array of bone rotations for this character as vec4[].

**boneScale <float[]>**: The array of bone scales for this character as vec3[].

### Texture Package

An texture package contains all data to create a texture. This data is referenced by materials stored in the Material Packages. For legacy reasons, textures can also be stored in Unity Texture format.   

<img src="/.doc/img/TexturePkg.png" height="150">


**colorMapDataSize \<int\>**: The size of the texture raw data in bytes.

**width \<int\>**: The width of the texture in pexels.

**height \<int\>**: The height of the texture in pixels.

**format \<byte\>**: Enumeration storing the textures format (0-59).

**texture <Texture (Unity type)>**: Optional, a texture stord in the Unity format. Can be empty.

**colorMapData <byte[]>**: The array of bytes storing the raw texture data.

### Material Package

A material package contains all the data needed to create a referenceable material. Exactly one material can be assigned to a geometry node. The material itself can reference any number of textures. Materials are divided into two different types. A Legacy Material (Type 1), which supports only one texture, including its UV coordinates. And a DX9 style (Type 0) with any number of texture slots, properties, and variables. The variables are stored in shader variable arrays and are then available within the shader. Currently, only precompiled shaders are supported, which must be available at the time the material is created. These are referenced via a shader name as a string.

<img src="/.doc/img/MaterialPkg.png" height="150">

**type \<int\>**: The type of the material. 0=Unity, 1=External

**name \<string\>**: The name of the material.

**src \<string\>**: The Unity resource name the material uses to refrence its shaders.

**materialID \<int\>**: The material ID for instancing.
 
**textureIds <int[]>**: The IDs for referencing the associated texture data.

**textureOffsets <float[]>**: The textures UV offset as Vec2.

**textureScales <float[]>**: The textures UV scale as Vec2.

**shaderConfig <bool[]>**: Available shader configuration flags. The positions in the array referencing DX9 shader configurations...\
**[0] "_NORMALMAP"\
[1] "_ALPHATEST_ON"\
[2] "_ALPHABLEND_ON"\
[3] "_ALPHAPREMULTIPLY_ON"\
[4] "_EMISSION"\
[5] "_PARALLAXMAP"\
[6] "_DETAIL_MULX2"\
[7] "_METALLICGLOSSMAP"\
[8] "_SPECGLOSSMAP"**

**shaderPropertyIds <int[]>**: Available shader property IDs ( ID, type (not part of this array) ). The respective number in the array refes to a specific property owned by a shader.\
**[0] "_Color, Color"\
[1] "_MainTex, Texture"\
[2] "_Cutoff, float"\
[3] "_Glossiness, float"\
[4] "_GlossMapScale, float"\
[5] "_SmoothnessTextureChannel, int"\
[6] "_Metallic, float"\
[7] "_MetallicGlossMap, Texture"\
[8] "_SpecularHighlights, int"\
[9] "_GlossyReflections, int"\
[10] "_BumpScale, int"\
[11] "_BumpMap, Texture"\
[12] "_Parallax, float"\
[13] "_ParallaxMap, Texture"\
[14] "_OcclusionStrength, float"\
[15] "_OcclusionMap, Texture"\
[16] "_EmissionColor, Color"\
[17] "_EmissionMap, Texture"\
[18] "_DetailMask, Texture"\
[19] "_DetailAlbedoMap, Texture"\
[20] "_DetailNormalMapScale, int"\
[21] "_DetailNormalMap, Texture"\
[22] "_UVSec, int"\
[23] "_Mode, int"\
[24] "_SrcBlend, int"\
[25] "_DstBlend, int"\
[26] "_ZWrite, int"**

**ShaderPropertyTypes <int[]>**: Available shader property types. Counterpart of the shaderPropertyIds, defining the type of the respective shader property.\
**[0] "Color"\
[1] "Vector4"\
[2] "int"\
[3] "float"\
[4] "TextureID"**

shaderProperties <byte[]>: The properties respective value as a byte stream.

### Parameter Package

The ParameterObjectPackage allows to generate parameters and their parent ParameterObjects at runtime, as well as save and load them in scenes.

<img src="/.doc/img/ParameterObjectPkg.png" height="150">


**id <int>**: The _id of the parameter object

**name <string>**: The name of the parameter object.

**pTypes <int[]>**: The types of the parameters inside the parameter object.

**pRPC <bool[]>**: Flag determines wether a parameter is an RPC parameter.

**pNames <string[]>**: The names of the parameters inside the parameter object.

## Menu System

TRACER also provides a “mini-markup language” for creating menu structures. The MenuTree class implements a tree structure and functions that allow nested tree structures—including layout and simple GUI elements—to be organized into menus directly within the source code. TRACER parameters can also be used directly by automatically linking to appropriate GUI elements based on their type. Using Unity as the Host Application, the code block below will be used to generate the menu shown in the image. The Parameter with the type Action links to a defined function called every time the Button will be clicked. A corresponding text box is generated for the manager's string parameter. All elements are listed in the specified hierarchy, sorted visually in horizontal and vertical alignment groups.  

<img src="/.doc/img/Menu.png" height="250">

```c#
 Parameter<Action> button = new Parameter<Action>(Connect, "Start");
 m_menu = new MenuTree()
    .Begin(MenuItem.IType.VSPLIT)
         .Begin(MenuItem.IType.HSPLIT)
             .Add("IP Address")
             .Add(manager.settings.ipAddress)
         .End()
         .Begin(MenuItem.IType.HSPLIT)
             .Add(button)
         .End()
   .End();

 m_menu.caption = "Scene Server Network Settings";
 m_menu.iconResourceLocation = "Images/button_network";
 core.getManager<UIManager>().addMenu(m_menu);
```

# Final Words

The menu system is a good example of where TRACER ends and how it integrates into a host application. It is now up to the developer to decide how to handle the abstract definition of a menu and its layout. This also makes TRACER’s portability between different hosts feasible, as the overhead required for integration is kept to a minimum. The basic implementation, including all the heavy lifting, will continue to take place in TRACER.
This completes the philosophy mentioned at the beginning, in which the respective modules and parameter objects handle the integration into the host application.

TRACER does not claim to be a solution to every problem. Rather, it is an effort to accelerate recurring developments and simplify the integration of a wide variety of applications. In addition, TRACER provides an ideal platform for making the knowledge and developments from our research projects accessible to everyone. 


