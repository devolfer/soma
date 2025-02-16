![Version](https://img.shields.io/badge/version-1.1.0-blue)
![License: MIT](https://img.shields.io/badge/license-MIT-green)

<img width="1200" alt="Image" src="https://github.com/user-attachments/assets/f6446358-8078-4b91-b38d-82c971990d76" />   

<br></b>
Includes:
* Play/Pause/Resume/Fade/Stop individual or all sounds
* Set/Mute/Fade volume of Audio Mixers
* Sound entities use object pooling under the hood
* Async/Await support (including [UniTask](https://github.com/Cysharp/UniTask)!)
* Easy to learn & powerful API
* Emitter & Volume Mixer components for non-coders

## Table of Contents
* [Getting started](#getting-started)
  * [Installation](#installation)
  * [UniTask](#unitask)
  * [Code Hints](#code-hints)
* [Sounds](#sounds)
  * [Play](#play)
  * [Pause and Resume](#pause-and-resume)
  * [Stop](#stop)
  * [Fade](#fade)
* [Audio Mixers](#audio-mixers)
  * [Mandatory Setup](#mandatory-setup)
  * [Register and Unregister Volume Group](#register-and-unregister-volume-group)
  * [Set Volume](#set-volume)
  * [Mute and Unmute Volume](#mute-and-unmute-volume)
  * [Fade Volume](#fade-volume)
* [Available Components](#available-components)
  * [Soma Emitter](#soma-emitter)
  * [Soma Volume Mixer](#soma-volume-mixer)
* [License](#license)

## Getting started
### Installation
Through the Package Manager in the Editor as a git package: `https://github.com/devolfer/soma.git`.   
The Package Manager can be opened under `Window -> Package Manager`.

<img width="640" alt="add-git-package-0" src="https://github.com/user-attachments/assets/e1e4ab90-fdc4-40e2-9768-3b23dc69f12b">
<img width="640" alt="add-git-package-1" src="https://github.com/user-attachments/assets/e01074af-0f7f-40aa-8151-adb980aca8e2" />

Or as `"com.devolfer.soma": "https://github.com/devolfer/soma.git"` in `Packages/manifest.json`.

Manual import into a folder is of course also possible.

### UniTask
Even if async/await workflow is not intended to be used, it is very favourable to install UniTask anyway.   
*Synchronous methods will be invoked as **allocation-free** tasks under the hood!*

The installation guide can be found in the [Official UniTask Repo](https://github.com/Cysharp/UniTask).   

Once installed, all async code of this package will automatically compile using `UniTask` instead of standard `C# Task`!   
This will potentially break any existing asynchronous code usage, that was initially expecting a C# Task return value.

### Code Hints
Using code hints is highly encouraged and can be enough to get a grasp of this package.   

To see them in an IDE, generating .csproj files for git packages must be enabled.  
This can be done by going to `Preferences|Settings -> External Tools`, marking the checkbox and regenerating the project files.

<img width="692" alt="preferences-external-tools-enable-git-packages" src="https://github.com/user-attachments/assets/f6d33702-c9ad-4fb7-97b7-c4e3cd8e24a3">

If in doubt, the following sections aim to provide as clear explanations and examples as possible. 

## Sounds
### Play
Playing a sound is as simple as calling the `Play` method and passing an `AudioClip` to it.

```csharp
using Devolfer.Soma;
using UnityEngine;

public class YourBehaviour : MonoBehaviour
{
    // Injects clip via Editor Inspector
    [SerializeField] private AudioClip audioClip;
    
    private void YourMethod()
    {
        // Plays clip through the Soma instance
        Soma.Play(audioClip);
        
        // *** There is no need for an AudioSource component.
        // Soma will retrieve a SomaEntity instance from its pool, 
        // play the clip through it, and then return it back to the pool. ***
    }
}
```

To alter the behaviour, there are various optional parameters that can be passed to the `Play` method.

```csharp
// Plays clip at world position
Soma.Play(audioClip, position: new Vector3(0, 4, 2));

// *** The above call is very similar to Unitys 'AudioSource.PlayClipAtPoint()' method,
// however, there is no overhead of instantiating & destroying a GameObject involved! ***  

// Injects follow target transform via Editor Inspector
[SerializeField] private Transform transformToFollow;

// Plays clip at local position while following 'transformToFollow'
Soma.Play(audioClip, followTarget: transformToFollow, position: new Vector3(0, 4, 2));

// Plays clip with fade in of 1 second and applies InSine easing
Soma.Play(audioClip, fadeIn: true, fadeInDuration: 1f, fadeInEase = Ease.InSine);

// Plays clip and prints log statement at completion
Soma.Play(audioClip, onComplete: () => Debug.Log("Yeah, this sound finished playing!"));
```

For any further custom sound settings, there is the `SomaProperties` class.   
It mimics the public properties of an `AudioSource` and allows control over e.g. volume & pitch.

```csharp
// Defines random volume
float volume = UnityEngine.Random.Range(.5f, 1f);

// Defines random pitch using pentatonic scale
float pitch = 1f;
int[] pentatonicSemitones = { 0, 2, 4, 7, 9 };
int amount = pentatonicSemitones[UnityEngine.Random.Range(0, pentatonicSemitones.Length)];
for (int i = 0; i < amount; i++) pitch *= 1.059463f;

// Plays via SomaProperties with clip, volume & pitch
Soma.Play(new SomaProperties(audioClip) { Volume = volume, Pitch = pitch });

// *** Passing new SomaProperties like above is just for demonstration.
// When possible those should be cached & reused! ***
```

It is also no problem to pass an `AudioSource` directly.

```csharp
// Injects AudioSource via Editor Inspector
[SerializeField] private AudioSource audioSource;

// Plays with 'audioSource' properties
Soma.Play(audioSource);

// Plays with 'audioSource' properties, but this time looped
Soma.Play(new SomaProperties(audioSource) { Loop = true });

// *** The call above passes an implicit SomaProperties copy of the AudioSource properties.
// This can be useful for selectively changing AudioSource properties at call of Play. ***
```

Playing a sound async can be done by calling the `PlayAsync` method.   
Its declaration looks very similar to all the above.

```csharp
private async void YourAsyncMethod()
{
    CancellationToken someCancellationToken = new();
        
    try
    {
        // Plays clip with default fade in
        await Soma.PlayAsync(audioClip, fadeIn: true);
            
        // Plays clip with cancellation token 'someCancellationToken'
        await Soma.PlayAsync(audioClip, cancellationToken: someCancellationToken);
        
        // *** Using tokens is optional, as each playing SomaEntity handles 
        // its own cancellation when needed. ***

        // Plays clip with SomaProperties at half volume & passes 'someCancellationToken'
        await Soma.PlayAsync(
            new SomaProperties(audioClip) { Volume = .5f },
            cancellationToken: someCancellationToken);
            
        // Plays with 'audioSource' properties
        await Soma.PlayAsync(audioSource);
            
        Debug.Log("Awaiting is done. All sounds have finished playing one after another!");
    }
    catch (OperationCanceledException _)
    {
        // Handle cancelling however needed
    }
}
```

### Pause and Resume
Pausing and resuming an individual sound requires to pass a `SomaEntity` or an `AudioSource`.   
The `Play` method returns a `SomaEntity`, `PlayAsync` optionally outs a `SomaEntity`.

```csharp
// Plays clip & caches playing SomaEntity into variable 'entity'
SomaEntity entity = Soma.Play(audioClip);

// Plays clip async & outs playing SomaEntity into variable 'entity'
await Soma.PlayAsync(out SomaEntity entity, audioClip);

// Doing the above with 'audioSource' properties
Soma.Play(audioSource);
await Soma.PlayAsync(audioSource);

// *** When calling Play with an AudioSource it is not mandatory to cache the playing SomaEntity.
// Soma will cache both in a Dictionary for later easy access! ***
```

Calling `Pause` and `Resume` can then be called on any playing sound.

```csharp
// Pauses & Resumes cached/outed 'entity'
Soma.Pause(entity);
Soma.Resume(entity);

// Pauses & Resumes via original `audioSource`
Soma.Pause(audioSource);
Soma.Resume(audioSource);

// Pauses & Resumes all sounds
Soma.PauseAll();
Soma.ResumeAll();

// *** A sound, that is in the process of stopping, cannot be paused! ***
```

### Stop
Stopping also requires to pass a `SomaEntity` or an `AudioSource`.

```csharp
// Stops both cached 'entity' & 'audioSource'
Soma.Stop(entity);
Soma.Stop(audioSource);

// Same as above as async call
await Soma.StopAsync(entity);
await Soma.StopAsync(audioSource);

// Stops all sounds
Soma.StopAll();
await Soma.StopAllAsync();
```

By default, the `Stop` and `StopAsync` methods fade out when stopping. This can be individually set.

```csharp
// Stops cached 'entity' with long fadeOut duration
Soma.Stop(
    entity, 
    fadeOutDuration: 3f, 
    fadeOutEase: Ease.OutSine, 
    onComplete: () => Debug.Log("Stopped sound after long fade out."));

// Stops cached 'entity' with no fade out
Soma.Stop(entity, fadeOut: false);

// Stops 'audioSource' async with default fade out
await Soma.StopAsync(audioSource, cancellationToken: someCancellationToken);
```

### Fade
For fading a sound, it is mandatory to set a `targetVolume` and `duration`.

```csharp
// Fades cached 'entity' to volume 0.2 over 1 second
Soma.Fade(entity, .2f, 1f);

// Pauses cached 'entity' & then fades it to full volume with InExpo easing over 0.5 seconds
Soma.Pause(entity);
Soma.Fade(
    entity, 
    1f, 
    .5f, 
    ease: Ease.InExpo, 
    onComplete: () => Debug.Log("Quickly faded in paused sound again!"));

// Fades 'audioSource' to volume 0.5 with default ease over 2 seconds
await Soma.FadeAsync(audioSource, .5f, 2f, cancellationToken: someCancellationToken);

// *** Stopping sounds cannot be faded and paused sounds will automatically resume when faded! ***    
```
---
The `CrossFade` and `CrossFadeAsync` methods provide ways to simultaneously fade two sounds out and in.   
This means, an existing sound will be stopped fading out, while a new one will play fading in.  

```csharp
// Cross-fades cached 'entity' & new clip over 1 second
SomaEntity newEntity = Soma.CrossFade(1f, entity, new SomaProperties(audioClip));

// Async cross-fades two sound entities & outs the new one
await Soma.CrossFadeAsync(out newEntity, 1f, entity, new SomaProperties(audioClip));

// *** The returned SomaEntity will be the newly playing one 
// and it will always fade in to full volume. ***
```

Simplified cross-fading might not lead to the desired outcome.   
If so, invoking two `Fade` calls simultaneously will grant finer fading control.

## Audio Mixers
### Mandatory Setup
*This section can be skipped, if the `AudioMixerDefault` asset included in this package suffices.*   

It consists of the groups `Master`, `Music` and `SFX`, with respective `Exposed Parameters`: `VolumeMaster`, `VolumeMusic` and `VolumeSFX`.

<img width="738" alt="audio-mixer-default" src="https://github.com/user-attachments/assets/9dbe0850-42ac-45a3-a6e1-06d89b9d02b1">

---
An `AudioMixer` is an asset that resides in the project folder and needs to be created and setup manually in the Editor.   
It can be created by right-clicking in the `Project Window` or under `Assets` and then `Create -> Audio Mixer`.

<img width="652" alt="create-audio-mixer-asset" src="https://github.com/user-attachments/assets/3b989593-3ab2-4a65-b5c9-3952d8c61566">

This will automatically create the `Master` group.   
To access the volume of a Mixer Group, an `Exposed Parameter` has to be created.   
Selecting the `Master` group and right-clicking on the volume property in the inspector allows exposing the parameter.

<img width="373" alt="select-mixer-group" src="https://github.com/user-attachments/assets/6fffcbbd-4ce4-4951-86b7-17c02e806d46">
<img width="522" alt="expose-mixer-volume-parameter" src="https://github.com/user-attachments/assets/e719c0d5-affb-4dd2-8998-c987bd1614c1">

Double-clicking the `Audio Mixer` asset or navigating to `Window -> Audio -> Audio Mixer` will open the `Audio Mixer Window`.   
Once opened, the name of the parameter can be changed under the `Exposed Parameters` dropdown by double-clicking it.   

***This is an important step! The name given here, is how the group will be globally accessible by `Soma`.***

<img width="240" alt="rename-mixer-parameter" src="https://github.com/user-attachments/assets/69fd3ca6-0596-46f0-b7d8-6f418c857597">

Any other custom groups must be added under the `Groups` section by clicking the `+` button.  

<img width="522" alt="add-mixer-group" src="https://github.com/user-attachments/assets/70648708-42f5-4400-81d8-e997fae00d08">


***Just like before, exposing the volume parameters manually is unfortunately a mandatory step!***

### Register and Unregister Volume Group
To let `Soma` know, which `AudioMixer` volume groups it should manage, they have to be registered and unregistered.   
This can be done via scripting or the Editor.

It is straightforward by code, however the methods expect an instance of type `SomaVolumeMixerGroup`.   
This is a custom class that provides various functionality for handling a volume group in an `AudioMixer`.

```csharp
// Injects AudioMixer asset via Editor Inspector
[SerializeField] private AudioMixer audioMixer;

// Creates a SomaVolumeMixerGroup instance with 'audioMixer' & the pre-setup exposed parameter 'VolumeMusic'
SomaVolumeMixerGroup group = new(audioMixer, "VolumeMusic", volumeSegments: 10);

// *** Volume segments can optionally be defined for allowing incremental/decremental volume change.
// This can e.g. be useful in segmented UI controls. *** 

// Registers & Unregisters 'group' with & from Soma
Soma.RegisterMixerVolumeGroup(group);
Soma.UnregisterMixerVolumeGroup(group);

// *** It is important, that the exposed parameter exists in the referenced AudioMixer.
// Otherwise an error will be thrown! ***
```
---
Registering via Editor can be done through the [Soma Volume Mixer](#soma-volume-mixer) component or the `Soma` instance in the scene.   

For the latter, right-clicking in the `Hierarchy` or under `GameObject` and then `Audio -> Soma` will create an instance.   

<img width="362" alt="add-sound-manager" src="https://github.com/user-attachments/assets/a446a32f-6e96-4656-b78a-e9577d76cea9">

Any groups can then be added in the list of `Mixer Volume Groups Default`.

<img width="534" alt="add-mixer-volume-group-inspector" src="https://github.com/user-attachments/assets/929c0555-7f7c-4bb8-8912-5c965358e8fa">

*If left empty, `Soma` will register and unregister the groups contained in the `AudioMixerDefault` asset automatically!*

### Set Volume
Setting a volume can only be done in percentage values (range 0 - 1).   
Increasing and decreasing in steps requires the volume segments of the group to be more than 1.

```csharp
// Sets volume of 'VolumeMusic' group to 0.5
Soma.SetMixerGroupVolume("VolumeMusic", .5f);

// Incrementally sets volume of 'VolumeMusic' group
// With volumeSegments = 10, this will result in a volume of 0.6
Soma.IncreaseMixerGroupVolume("VolumeMusic");

// Decrementally sets volume of 'VolumeMusic' group
// With volumeSegments = 10, this will result in a volume of 0.5 again
Soma.DecreaseMixerGroupVolume("VolumeMusic");
```

### Mute and Unmute Volume
Muting and unmuting sets the volume to a value of 0 or restores the previously stored unmuted value.

```csharp
// Sets volume of 'VolumeMusic' group to 0.8
Soma.SetMixerGroupVolume("VolumeMusic", .8f);

// Mutes volume of 'VolumeMusic' group
Soma.MuteMixerGroupVolume("VolumeMusic", true);

// Equivalent to above
Soma.SetMixerGroupVolume("VolumeMusic", 0f);

// Unmutes volume of 'VolumeMusic' group back to value 0.8
Soma.MuteMixerGroupVolume("VolumeMusic", false);
```

### Fade Volume
Fading requires a `targetVolume` and `duration`.

```csharp
// Fades volume of 'VolumeMusic' group to 0 over 2 seconds
Soma.FadeMixerGroupVolume("VolumeMusic", 0f, 2f);

// Same as above but with InOutCubic easing & onComplete callback
Soma.FadeMixerGroupVolume(
    "VolumeMusic", 0f, 2f, ease: InOutCubic, onComplete: () => Debug.Log("Volume was smoothly muted!"));

// Similar to above as async call
await Soma.FadeMixerGroupVolumeAsync(
    "VolumeMusic", 0f, 2f, ease: InOutCubic, cancellationToken: someCancellationToken);

Debug.Log("Volume was smoothly muted!")
```

Simplified linear cross-fading is also supported.   
It will fade out the first group to a volume of 0 and fade in the other to 1.   

```csharp
// Fades volume of 'VolumeMusic' out & fades in 'VolumeDialog' over 1 second
Soma.CrossFadeMixerGroupVolumes("VolumeMusic", "VolumeDialog", 1f);

// Same as above as async call
await Soma.CrossFadeMixerGroupVolumesAsync("VolumeMusic", "VolumeDialog", 1f);
```

For any finer controlled cross-fading, it is recommended to call multiple fades simultaneously.

## Available Components
### Soma Emitter
The `Soma Emitter` component is a simple way of adding a sound, that is handled by `Soma`.   
It can be attached to any gameObject or created by right-clicking in the `Hierarchy` or under `GameObject` and then `Audio -> Soma Emitter`.

<img width="362" alt="add-sound-emitter" src="https://github.com/user-attachments/assets/8828558b-41a5-4e56-ad3d-6c3d3785e2e1">

An `AudioSource` component will automatically be added if not already present.   
Through it and the `Configurations` `Play`, `Stop` and `Fade` it is possible to define the sound behaviour in detail.

<img width="321" alt="sound-emitter-overview" src="https://github.com/user-attachments/assets/092573ec-a987-4a8e-9cf2-dfa85bcc668e">
 
The component grants access to the following public methods:
* **Play**: Starts sound playback, if not already playing.
* **Pause**: Interrupts sound playback, if not currently stopping.
* **Resume**: Continues sound playback, if paused and not currently stopping.
* **Fade**: Fades volume of currently playing sound to the given target volume.
* **Stop**: Stops sound playback before completion (the only way, when looped).

***Calling the methods will always apply the respective configurations. It is therefore important to set them before entering `Play Mode`!***   

The image below shows an example usage of a `Button` component and how one could invoke the methods via the `onClick` event.

<img width="580" alt="sound-emitter-public-methods" src="https://github.com/user-attachments/assets/da049164-8c4f-4b8b-b5e8-61f0c3aef5da">

### Soma Volume Mixer
A `Soma Volume Mixer` simplifies volume mixing of an `Audio Mixer` group and uses the `Soma` for it.   
This component can be added to any GameObject in the scene.   
Or by creating it via right-clicking in the `Hierarchy` or using the `GameObject` menu, then choosing `Audio -> Soma Volume Mixer`.

<img width="363" alt="add-sound-volume-mixer" src="https://github.com/user-attachments/assets/eca71cd0-d8e8-44b7-ae38-e01374ed1014">

For the component to work, a reference to the `Audio Mixer` asset is mandatory and the `Exposed Parameter` name of the group has to be defined.   
Section [Mandatory Setup](#mandatory-setup) explains how to create such group and expose a parameter for it.

<img width="383" alt="sound-volume-mixer-overview" src="https://github.com/user-attachments/assets/112d5418-32ea-4c52-acb8-a2ee796d7dca">

The following methods of the component are usable:
* **Set**: Changes group volume to the given value instantly.
* **Increase**: Instantly raises group volume step-wise (e.g. for `Volume Segments = 10`: +10%).
* **Decrease**: Instantly lowers group volume step-wise (e.g. for `Volume Segments = 10`: -10%).
* **Fade**: Fades group volume to the given target volume.
* **Mute**: Either mutes or un-mutes group volume.

***`Fade Configuration` determines, how volume fading will behave and must be setup before entering `Play Mode`!***   

An example usage of the above methods can be seen below with a `Button` component and its `onClick` event.

<img width="600" alt="sound-volume-mixer-public-methods" src="https://github.com/user-attachments/assets/3354934d-fb70-4725-9793-6e5d39cbe851">

## License
This package is under the MIT License.
