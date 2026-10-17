using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;

namespace EchoCharacters.Editor
{
    /// <summary>Explicit tools only: nothing runs automatically in an existing project.</summary>
    public static class EchoCharacterBuilder
    {
        const string Root = "Assets/EchoCharacters";
        const string Model = Root + "/Models/EchoCharacter.fbx";
        static readonly string[] ClipNames = {"BriskWalk","DoubleAppear","DoubleIdle","DoubleRush","DoubleTalk","DoubleVanish","FallLoop","GazeUp","HitReact","Idle","Interact","JumpAnticipation","JumpAnticipationMoving","JumpApex","JumpForwardTakeoff","JumpFromStrideLeft","JumpFromStrideRight","JumpRise","JumpStart","JumpTakeoff","Land","LandHeavy","LandIntoRunLeft","LandIntoRunRight","LandIntoStrideLeft","LandIntoStrideLeftBackward","LandIntoStrideLeftBackwardLeft","LandIntoStrideLeftBackwardRight","LandIntoStrideLeftForwardLeft","LandIntoStrideLeftForwardRight","LandIntoStrideLeftStrafeLeft","LandIntoStrideLeftStrafeRight","LandIntoStrideRight","LandIntoStrideRightBackward","LandIntoStrideRightBackwardLeft","LandIntoStrideRightBackwardRight","LandIntoStrideRightForwardLeft","LandIntoStrideRightForwardRight","LandIntoStrideRightStrafeLeft","LandIntoStrideRightStrafeRight","LandSoft","LandToBackward","LandToBackwardLeft","LandToBackwardRight","LandToForwardLeft","LandToForwardRight","LandToStrafe","LandToStrafeLeft","LandToStrafeRight","LandToWalk","LandTurn","LookAround","Nod","PivotLeft","PivotRight","PowerWalk","ReachDoor","ReachOut","ResolveIdle","Run","RunJump","RunJumpLeft","RunJumpRight","RunStart","RunStopLeft","RunStopRight","RunStrideApexLeft","RunStrideApexRight","RunStrideFallLeft","RunStrideFallRight","RunStrideRiseLeft","RunStrideRiseRight","RunToWalk","RunToWalkLeft","RunToWalkRight","ShakeHead","Sigh","StartBackward","StartBackwardLeft","StartBackwardLeftPower","StartBackwardPower","StartBackwardRight","StartBackwardRightPower","StartForward","StartForwardLeft","StartForwardLeftPower","StartForwardPower","StartForwardRight","StartForwardRightPower","StartLeft","StartLeftPower","StartRight","StartRightPower","Startle","StrafeLeft","StrafeLeftBrisk","StrafeLeftPower","StrafeRight","StrafeRightBrisk","StrafeRightPower","StrideApexLeft","StrideApexRight","StrideFallLeft","StrideFallRight","StrideRiseLeft","StrideRiseRight","Talk","TalkGesture1","TalkGesture2","TalkGesture3","Turn180Left","Turn180Right","Turn45Left","Turn45Right","Turn90Left","Turn90Right","TurnLeft","TurnRight","WakeIdle","Walk","WalkBackward","WalkBackwardBrisk","WalkBackwardLeft","WalkBackwardLeftBrisk","WalkBackwardLeftPower","WalkBackwardPower","WalkBackwardRight","WalkBackwardRightBrisk","WalkBackwardRightPower","WalkBrisk","WalkForward","WalkForwardBrisk","WalkForwardLeft","WalkForwardLeftBrisk","WalkForwardLeftPower","WalkForwardPower","WalkForwardRight","WalkForwardRightBrisk","WalkForwardRightPower","WalkNormal","WalkStart","WalkStopLeft","WalkStopLeftBackward","WalkStopLeftBackwardLeft","WalkStopLeftBackwardRight","WalkStopLeftForwardLeft","WalkStopLeftForwardRight","WalkStopLeftStrafeLeft","WalkStopLeftStrafeRight","WalkStopRight","WalkStopRightBackward","WalkStopRightBackwardLeft","WalkStopRightBackwardRight","WalkStopRightForwardLeft","WalkStopRightForwardRight","WalkStopRightStrafeLeft","WalkStopRightStrafeRight","WalkToRun","WalkToRunLeft","WalkToRunRight"};
        static readonly string[] PoseBoneNames = {"Root","Hips","Spine","Chest","Neck","Head","LeftEye","RightEye","HairBangLeft_01","HairBangLeft_02","HairBangLeft_03","HairSideLeft_01","HairSideLeft_02","HairSideLeft_03","HairBackLeft_01","HairBackLeft_02","HairBackLeft_03","EarringLeft_01","EarringLeft_02","HairBangRight_01","HairBangRight_02","HairBangRight_03","HairSideRight_01","HairSideRight_02","HairSideRight_03","HairBackRight_01","HairBackRight_02","HairBackRight_03","EarringRight_01","EarringRight_02","LeftShoulder","LeftUpperArm","LeftLowerArm","LeftHand","SleeveLeft_01","SleeveLeft_02","RightShoulder","RightUpperArm","RightLowerArm","RightHand","SleeveRight_01","SleeveRight_02","RibbonWingLeft_01","RibbonWingLeft_02","RibbonTailLeft_01","RibbonTailLeft_02","RibbonWingRight_01","RibbonWingRight_02","RibbonTailRight_01","RibbonTailRight_02","ShirtCollarLeft_01","ShirtCollarRight_01","ShirtHemFrontLeft_01","ShirtHemFrontLeft_02","ShirtHemSideLeft_01","ShirtHemSideLeft_02","ShirtHemBackLeft_01","ShirtHemBackLeft_02","ShirtHemBackRight_01","ShirtHemBackRight_02","ShirtHemSideRight_01","ShirtHemSideRight_02","ShirtHemFrontRight_01","ShirtHemFrontRight_02","LeftUpperLeg","LeftLowerLeg","LeftFoot","LeftToes","SkirtFrontLeft","SkirtBackLeft","RightUpperLeg","RightLowerLeg","RightFoot","RightToes","SkirtFrontRight","SkirtBackRight","SkirtChain00_01","SkirtChain00_02","SkirtChain00_03","SkirtChain01_01","SkirtChain01_02","SkirtChain01_03","SkirtChain02_01","SkirtChain02_02","SkirtChain02_03","SkirtChain03_01","SkirtChain03_02","SkirtChain03_03","SkirtChain04_01","SkirtChain04_02","SkirtChain04_03","SkirtChain05_01","SkirtChain05_02","SkirtChain05_03","SkirtChain06_01","SkirtChain06_02","SkirtChain06_03","SkirtChain07_01","SkirtChain07_02","SkirtChain07_03"};
        static readonly string[] BoneNames = {"Hips","Spine","Chest","Neck","Head","LeftShoulder","LeftUpperArm","LeftLowerArm","LeftHand","RightShoulder","RightUpperArm","RightLowerArm","RightHand","LeftUpperLeg","LeftLowerLeg","LeftFoot","LeftToes","RightUpperLeg","RightLowerLeg","RightFoot","RightToes","LeftEye","RightEye"};
        sealed class ExtraCurve { public EditorCurveBinding binding; public AnimationCurve curve; }
        static readonly Dictionary<string,List<ExtraCurve>> SecondaryCurves = new Dictionary<string,List<ExtraCurve>>();
        static bool IsSecondaryPath(string path)
        {
            string name=path.Substring(path.LastIndexOf('/')+1);
            return name.StartsWith("ShirtHem",StringComparison.Ordinal)||name.StartsWith("Sleeve",StringComparison.Ordinal)||name.StartsWith("ShirtCollar",StringComparison.Ordinal)||name.StartsWith("Skirt",StringComparison.Ordinal)||name.StartsWith("HairBang",StringComparison.Ordinal)||name.StartsWith("HairSide",StringComparison.Ordinal)||name.StartsWith("HairBack",StringComparison.Ordinal)||name.StartsWith("RibbonWing",StringComparison.Ordinal)||name.StartsWith("RibbonTail",StringComparison.Ordinal)||name.StartsWith("Earring",StringComparison.Ordinal);
        }

        [MenuItem("Tools/Echo Characters/Rebuild Character Assets")]
        public static void Build()
        {
            ConfigureModel();
            var clips = ExtractClips();
            var normal = BuildMaterials(false);
            var dark = BuildMaterials(true);
            BuildPrefab(false, normal, BuildController(false, clips));
            BuildPrefab(true, dark, BuildController(true, clips));
            AssetDatabase.SaveAssets();
            Debug.Log("Echo Characters: built both prefabs, materials, humanoid avatar, and animation controllers.");
        }

        static void ConfigureModel()
        {
            var imp = (ModelImporter)AssetImporter.GetAtPath(Model);
            if (imp == null) throw new Exception("EchoCharacter.fbx is missing.");
            imp.animationType = ModelImporterAnimationType.Generic;
            imp.clipAnimations=new ModelImporterClipAnimation[0]; // Import every take before capturing the source curves.
            imp.importAnimation = true;imp.animationCompression=ModelImporterAnimationCompression.Off;imp.resampleCurves=true;
            imp.importCameras = false; imp.importLights = false;
            imp.materialImportMode = ModelImporterMaterialImportMode.None;
            imp.optimizeGameObjects = false;
            imp.meshCompression = ModelImporterMeshCompression.Off;
            imp.skinWeights = ModelImporterSkinWeights.Custom;
            imp.maxBonesPerVertex = 4;
            imp.globalScale = 1;
            imp.isReadable = true;
            imp.SaveAndReimport();
            // Retain transform curves for non-Humanoid chains before converting
            // the importer to Human. The body muscle curves are extracted later.
            SecondaryCurves.Clear();
            foreach(var clip in AssetDatabase.LoadAllAssetsAtPath(Model).OfType<AnimationClip>())
            {
                if(clip.name.StartsWith("__preview__")||clip.name.StartsWith("Key|",StringComparison.Ordinal))continue;
                string clean=ClipNames.FirstOrDefault(n=>clip.name==n||clip.name.EndsWith("|"+n,StringComparison.Ordinal));
                if(clean==null)continue;
                var curves=new List<ExtraCurve>();
                foreach(var binding in AnimationUtility.GetCurveBindings(clip))
                    if(binding.type==typeof(Transform)&&(PoseBoneNames.Contains(binding.path.Substring(binding.path.LastIndexOf('/')+1))))
                    {
                        var source=AnimationUtility.GetEditorCurve(clip,binding);
                        var targetBinding=binding;
                        if(IsSecondaryPath(binding.path))curves.Add(new ExtraCurve{binding=binding,curve=new AnimationCurve(source.keys){preWrapMode=source.preWrapMode,postWrapMode=source.postWrapMode}});targetBinding.path="_EchoAuthoredPose/"+string.Join("/",binding.path.Split('/').Select(part=>"Pose_"+part));
                        curves.Add(new ExtraCurve{binding=targetBinding,curve=new AnimationCurve(source.keys){preWrapMode=source.preWrapMode,postWrapMode=source.postWrapMode}});
                    }
                if(curves.Any(c=>c.binding.path.StartsWith("_EchoAuthoredPose/",StringComparison.Ordinal)))SecondaryCurves[clean]=curves;
            }
            foreach(var name in ClipNames)
                if(!SecondaryCurves.TryGetValue(name,out var captured)||!captured.Any(c=>c.binding.path.StartsWith("_EchoAuthoredPose/",StringComparison.Ordinal)))throw new Exception("Source pose curves missing for "+name);
            var instance = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Model));
            try
            {
                instance.name = AssetDatabase.LoadAssetAtPath<GameObject>(Model).name;
                var transforms = instance.GetComponentsInChildren<Transform>(true);
                Debug.Log("ECHO_BIND Head="+transforms.First(t=>t.name=="Head").position+" LeftHand="+transforms.First(t=>t.name=="LeftHand").position);
                var bones = BoneNames.Select(name => new HumanBone {
                    boneName = name,
                    humanName = HumanTrait.BoneName.First(h => h.Replace(" ", "") == name),
                    limit = new HumanLimit {useDefaultValues = true}
                }).ToArray();
                foreach (var b in bones)
                    if (!transforms.Any(t => t.name == b.boneName)) throw new Exception("Missing bone " + b.boneName);
                var hd = new HumanDescription {
                    human = bones,
                    skeleton = transforms.Select(t => new SkeletonBone {name=t.name, position=t.localPosition, rotation=t.localRotation, scale=t.localScale}).ToArray(),
                    upperArmTwist=.5f, lowerArmTwist=1f, upperLegTwist=.5f, lowerLegTwist=.5f,
                    armStretch=0f, legStretch=0f, feetSpacing=0, hasTranslationDoF=false
                };
                imp.animationType = ModelImporterAnimationType.Human;
                imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                imp.humanDescription = hd;
                var defaults = imp.defaultClipAnimations.Where(c=>!c.takeName.StartsWith("Key|",StringComparison.Ordinal)).ToArray();
                foreach (var clip in defaults)
                {
                    string clean = ClipNames.FirstOrDefault(n => clip.name == n || clip.name.EndsWith("|" + n) || clip.name.EndsWith("_" + n));
                    if (clean == null) clean = ClipNames.FirstOrDefault(n => clip.takeName.EndsWith("|" + n) || clip.takeName == n);
                    if (clean != null) clip.name=clean;
                    clip.loopTime = clean != null && (clean=="Idle"||clean=="Talk"||clean=="DoubleIdle"||clean=="DoubleTalk"||clean=="ResolveIdle"||clean=="WakeIdle"||clean.StartsWith("TalkGesture")||clean=="JumpRise"||clean=="FallLoop"||clean.StartsWith("StrideRise")||clean.StartsWith("StrideFall")||clean.StartsWith("RunStrideRise")||clean.StartsWith("RunStrideFall")||clean=="PowerWalk"||clean=="BriskWalk"||clean=="Run"||clean=="DoubleRush"||(clean.StartsWith("Walk")&&!clean.Contains("Start")&&!clean.Contains("Stop")&&!clean.StartsWith("WalkToRun"))||clean.StartsWith("Strafe"));
                    clip.loopPose = clip.loopTime;
                    clip.lockRootRotation=true;clip.lockRootHeightY=true;clip.lockRootPositionXZ=true;
                    clip.keepOriginalOrientation=true;clip.keepOriginalPositionY=true;clip.keepOriginalPositionXZ=true;
                }
                imp.clipAnimations=defaults;
                imp.SaveAndReimport();
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); }
            var avatar = AssetDatabase.LoadAllAssetsAtPath(Model).OfType<Avatar>().FirstOrDefault();
            if (!avatar || !avatar.isValid || !avatar.isHuman) throw new Exception("Humanoid avatar validation failed.");
        }

        static Dictionary<string, AnimationClip> ExtractClips()
        {
            var imported=AssetDatabase.LoadAllAssetsAtPath(Model).OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__preview__")).ToArray();
            var result=new Dictionary<string,AnimationClip>();
            foreach (var name in ClipNames)
            {
                var source=imported.FirstOrDefault(c=>c.name==name);
                if (!source) throw new Exception("Animation missing: "+name+"; imported: "+string.Join(",",imported.Select(c=>c.name)));
                string path=Root+"/Animations/"+name+".anim";
                var target=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (!target) {target=UnityEngine.Object.Instantiate(source);target.name=name;AssetDatabase.CreateAsset(target,path);}
                else EditorUtility.CopySerialized(source,target);
                var meshPaths=AssetDatabase.LoadAssetAtPath<GameObject>(Model).GetComponentsInChildren<SkinnedMeshRenderer>(true).Select(r=>AnimationUtility.CalculateTransformPath(r.transform,AssetDatabase.LoadAssetAtPath<GameObject>(Model).transform)).ToHashSet();var meshCurves=AnimationUtility.GetCurveBindings(target).Where(b=>b.type==typeof(Transform)&&meshPaths.Contains(b.path)).ToArray();if(meshCurves.Length>0)AnimationUtility.SetEditorCurves(target,meshCurves,new AnimationCurve[meshCurves.Length]);
                if(SecondaryCurves.TryGetValue(name,out var extra))
                    AnimationUtility.SetEditorCurves(target,extra.Select(curve=>curve.binding).ToArray(),extra.Select(curve=>curve.curve).ToArray());
                AddHandCurves(target,name);AddContactCurves(target,name);CompactConstantCurves(target);target.name=name;EditorUtility.SetDirty(target);result[name]=target;
            }
            return result;
        }

        static void AddHandCurves(AnimationClip clip,string name)
        {
            var model=AssetDatabase.LoadAssetAtPath<GameObject>(Model);
            var body=model.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(r=>r.name=="Body");
            string path=AnimationUtility.CalculateTransformPath(body.transform,model.transform);
                bool running=name.StartsWith("Run")||name=="DoubleRush";
            for(int i=0;i<body.sharedMesh.blendShapeCount;i++)
            {
                string shape=body.sharedMesh.GetBlendShapeName(i);
                if(!shape.Contains("HandRelaxed")&&!shape.Contains("HandSoftGrip"))continue;
                bool grip=shape.Contains("SoftGrip");
                float value=name=="ResolveIdle"?(grip?70:10):name=="WakeIdle"&&shape.EndsWith("Left")?(grip?65:10):(name.Contains("Power")||name=="PowerWalk")?(grip?60:25):(name.Contains("Brisk"))?(grip?45:40):grip?(running?100:30):(running?0:55);
                var curve=name=="DoubleVanish"?AnimationCurve.EaseInOut(0,grip?100:0,clip.length,grip?20:60):AnimationCurve.Constant(0,clip.length,value);
                if(name.StartsWith("JumpFromStride")||name.StartsWith("Stride"))curve=AnimationCurve.Constant(0,clip.length,grip?60:25);
                if(name.StartsWith("LandIntoStride"))curve=AnimationCurve.Constant(0,clip.length,grip?60:25);
                if(name.StartsWith("WalkToRun")||name.StartsWith("RunToWalk"))curve=AnimationCurve.EaseInOut(0,name.StartsWith("WalkToRun")?(grip?60:25):(grip?100:0),clip.length,name.StartsWith("WalkToRun")?(grip?100:0):(grip?60:25));
                if(name.StartsWith("TalkGesture",StringComparison.Ordinal)&&(name=="TalkGesture3"||(name=="TalkGesture2"?shape.EndsWith("Left"):shape.EndsWith("Right"))))curve=new AnimationCurve(new Keyframe(0,value),new Keyframe(clip.length*.35f,grip?0:65),new Keyframe(clip.length*.7f,grip?0:65),new Keyframe(clip.length,value));
                if((name=="Interact"||name=="ReachOut"||name=="ReachDoor")&&shape.EndsWith("Right"))
                    curve=new AnimationCurve(new Keyframe(0,value),new Keyframe(clip.length*.35f,0),new Keyframe(clip.length*.7f,0),new Keyframe(clip.length,value));
                AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(path,typeof(SkinnedMeshRenderer),"blendShape."+shape),curve);
            }
        }
        [Serializable] sealed class ContactKey {public float time,leftContact,rightContact,leftPivot,rightPivot;}
        [Serializable] sealed class ContactClip {public string name;public ContactKey[] keys;}
        [Serializable] sealed class ContactFile {public ContactClip[] clips;}
        static void AddContactCurves(AnimationClip clip,string name)
        {
            var data=JsonUtility.FromJson<ContactFile>(AssetDatabase.LoadAssetAtPath<TextAsset>(Root+"/Editor/FootContacts.json").text);
            var row=data.clips.Single(c=>c.name==name);
            foreach(var field in new[]{"leftContact","rightContact","leftPivot","rightPivot"})
            {
                var keys=row.keys.Select(k=>new Keyframe(k.time,field=="leftContact"?k.leftContact:field=="rightContact"?k.rightContact:field=="leftPivot"?k.leftPivot:k.rightPivot)).ToArray();
                var curve=new AnimationCurve(keys);for(int i=0;i<curve.length;i++){AnimationUtility.SetKeyLeftTangentMode(curve,i,AnimationUtility.TangentMode.Linear);AnimationUtility.SetKeyRightTangentMode(curve,i,AnimationUtility.TangentMode.Linear);}
                bool idleContact=(name=="Idle"||name=="DoubleIdle")&&field.EndsWith("Contact",StringComparison.Ordinal);
                AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve("",typeof(EchoContactFeet),field),idleContact?AnimationCurve.Constant(0,clip.length,0):curve);
                if(field.EndsWith("Contact",StringComparison.Ordinal))AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve("",typeof(Animator),field=="leftContact"?"FootContactL":"FootContactR"),curve);
            }
            var events=new List<AnimationEvent>();
            if(name.StartsWith("LandInto",StringComparison.Ordinal))events.Add(new AnimationEvent{time=0,functionName=name.StartsWith("LandIntoStrideLeft",StringComparison.Ordinal)||name=="LandIntoRunLeft"?"FootL":"FootR"});
            foreach(bool left in new[]{true,false})
            {
                var first=row.keys[AnimationUtility.GetAnimationClipSettings(clip).loopTime?row.keys.Length-2:0];float previous=left?first.leftContact:first.rightContact;
                foreach(var key in row.keys){if(AnimationUtility.GetAnimationClipSettings(clip).loopTime&&key.time>=clip.length-.00001f)continue;float value=left?key.leftContact:key.rightContact;if(value>=.5f&&previous<.5f)events.Add(new AnimationEvent{time=key.time,functionName=left?"FootL":"FootR"});previous=value;}
            }
            if(name=="JumpTakeoff"||name=="JumpForwardTakeoff"||name.StartsWith("JumpFromStride")||name.StartsWith("RunJump"))events.Add(new AnimationEvent{time=(name.StartsWith("JumpFromStride")||name.StartsWith("RunJump"))?.10f:0,functionName="JumpTakeoff"});
            if(name.StartsWith("Land",StringComparison.Ordinal))events.Add(new AnimationEvent{time=0,functionName="Land"});
            AnimationUtility.SetAnimationEvents(clip,events.OrderBy(e=>e.time).ToArray());
        }
        static void CompactConstantCurves(AnimationClip clip)
        {
            var compactBindings=new List<EditorCurveBinding>();var compactCurves=new List<AnimationCurve>();
            foreach(var binding in AnimationUtility.GetCurveBindings(clip))
            {
                if(!binding.path.StartsWith("_EchoAuthoredPose/",StringComparison.Ordinal)&&!IsSecondaryPath(binding.path))continue;
                var curve=AnimationUtility.GetEditorCurve(clip,binding);var keys=curve.keys;if(keys.Length<3)continue;
                if(keys.All(k=>Mathf.Abs(k.value-keys[0].value)<.000001f)){compactBindings.Add(binding);compactCurves.Add(AnimationCurve.Constant(0,clip.length,keys[0].value));}
            }
            if(compactBindings.Count>0)AnimationUtility.SetEditorCurves(clip,compactBindings.ToArray(),compactCurves.ToArray());
        }
        static void ConfigureTurnCloth(EchoTurnFootIK turn,GameObject model)
        {
            var all=model.GetComponentsInChildren<Transform>(true);
            var lowerNames=new HashSet<string>{"Hips","LeftUpperLeg","LeftLowerLeg","LeftFoot","LeftToes","RightUpperLeg","RightLowerLeg","RightFoot","RightToes"};
            var bones=all.Where(t=>lowerNames.Contains(t.name)||t.name.StartsWith("SkirtChain",StringComparison.Ordinal)||t.name.StartsWith("ShirtHem",StringComparison.Ordinal)).OrderBy(t=>t.name,StringComparer.Ordinal).ToArray();
            var profiles=new List<EchoTurnFootIK.ClothProfile>();
            foreach(var item in new[]{("Turn45Left",-45f),("Turn45Right",45f),("TurnLeft",-90f),("TurnRight",90f),("Turn180Left",-180f),("Turn180Right",180f)})
            {
                var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(Root+"/Animations/"+item.Item1+".anim");
                if(!clip)throw new Exception("Missing turn cloth clip "+item.Item1);
                var bindings=AnimationUtility.GetCurveBindings(clip);int frames=Mathf.RoundToInt(clip.length*30)+1;
                var profile=new EchoTurnFootIK.ClothProfile{angle=item.Item2,frames=frames,rotations=new Quaternion[frames*bones.Length],positions=new Vector3[frames*bones.Length]};
                for(int i=0;i<bones.Length;i++)
                {
                    var reference=all.Single(t=>t.name=="Pose_"+bones[i].name);var q=reference.localRotation;var p=reference.localPosition;
                    var curves=new AnimationCurve[7];string[] properties={"m_LocalRotation.x","m_LocalRotation.y","m_LocalRotation.z","m_LocalRotation.w","m_LocalPosition.x","m_LocalPosition.y","m_LocalPosition.z"};
                    for(int c=0;c<properties.Length;c++)
                    {
                        var property=properties[c];var matches=bindings.Where(b=>b.type==typeof(Transform)&&b.path.EndsWith("/Pose_"+bones[i].name,StringComparison.Ordinal)&&b.propertyName==property).ToArray();
                        if(matches.Length==1)curves[c]=AnimationUtility.GetEditorCurve(clip,matches[0]);
                    }
                    if(curves.Take(4).Any(c=>c==null))throw new Exception("Incomplete turn cloth quaternion: "+item.Item1+" / "+bones[i].name);
                    for(int f=0;f<frames;f++)
                    {
                        float time=clip.length*f/(frames-1);var values=new[]{q.x,q.y,q.z,q.w,p.x,p.y,p.z};
                        for(int c=0;c<7;c++)if(curves[c]!=null)values[c]=curves[c].Evaluate(time);
                        profile.rotations[f*bones.Length+i]=new Quaternion(values[0],values[1],values[2],values[3]).normalized;
                        profile.positions[f*bones.Length+i]=new Vector3(values[4],values[5],values[6]);
                    }
                }
                profiles.Add(profile);
            }
            turn.ConfigureCloth(bones,profiles.ToArray());
        }


        static void ConfigureFeet(GameObject model)
        {
            model.AddComponent<EchoMotionAdditive>();model.AddComponent<EchoAnimationEvents>();var feet=model.AddComponent<EchoContactFeet>();var a=model.GetComponent<Animator>();
            var l=a.GetBoneTransform(HumanBodyBones.LeftFoot);var r=a.GetBoneTransform(HumanBodyBones.RightFoot);
            var heel=new Vector3(0,-.105f,-.045f);var toe=new Vector3(0,-.105f,.160f);
            feet.leftHeel=l.InverseTransformVector(model.transform.TransformVector(heel));feet.leftToe=l.InverseTransformVector(model.transform.TransformVector(toe));
            feet.rightHeel=r.InverseTransformVector(model.transform.TransformVector(heel));feet.rightToe=r.InverseTransformVector(model.transform.TransformVector(toe));
            feet.leftForward=l.InverseTransformDirection(model.transform.forward);feet.rightForward=r.InverseTransformDirection(model.transform.forward);
        }
        static Dictionary<string,Material> BuildMaterials(bool dark)
        {
            var result=new Dictionary<string,Material>();
            foreach(string name in new[]{"Body","Shirt","Shoes","Skirt","Hair","HairBase","Glasses","GlassesLens","Eyes","Ribbon","Earrings","Buttons"})
            {
                var path=Root+"/Materials/"+(dark?"Double_":"Hero_")+name+".mat";
                var material=AssetDatabase.LoadAssetAtPath<Material>(path);if(!material)throw new Exception("v8 is a patch: existing U1/S1 material required at "+path);
                result[name]=material;
            }
            return result;
        }

        [Serializable] sealed class SpeedClip {public float speed,duration;public float[] direction;}
        static AnimatorController BuildController(bool dark,Dictionary<string,AnimationClip> clips)
        {
            string path=Root+"/Animations/"+(dark?"TheDouble":"Protagonist")+".controller";
            var c=AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if(!c)c=AnimatorController.CreateAnimatorControllerAtPath(path);
            else
            {
                c.layers=Array.Empty<AnimatorControllerLayer>();c.parameters=Array.Empty<AnimatorControllerParameter>();
                foreach(var sub in AssetDatabase.LoadAllAssetsAtPath(path))if(sub&&sub!=c)UnityEngine.Object.DestroyImmediate(sub,true);
                var machine=new AnimatorStateMachine{name="Base Layer"};AssetDatabase.AddObjectToAsset(machine,c);c.layers=new[]{new AnimatorControllerLayer{name="Base Layer",defaultWeight=1,stateMachine=machine,iKPass=true}};
            }
            foreach(string name in new[]{"Speed","MoveX","MoveY","FootContactL","FootContactR"})c.AddParameter(name,AnimatorControllerParameterType.Float);
            c.AddParameter(new AnimatorControllerParameter{name="LocomotionRate",type=AnimatorControllerParameterType.Float,defaultFloat=1});
            foreach(string name in new[]{"Talking","Rushing","Running"})c.AddParameter(name,AnimatorControllerParameterType.Bool);
            foreach(string name in new[]{"Interact","LookAround","Appear"})c.AddParameter(name,AnimatorControllerParameterType.Trigger);
            var layers=c.layers;layers[0].iKPass=true;c.layers=layers;var sm=c.layers[0].stateMachine;
            var loco=sm.AddState("Locomotion");var tree=new BlendTree{name="FPS local velocity (model m/s)",blendType=BlendTreeType.FreeformDirectional2D,blendParameter="MoveX",blendParameterY="MoveY",useAutomaticThresholds=false};AssetDatabase.AddObjectToAsset(tree,c);
            tree.AddChild(clips[dark?"DoubleIdle":"Idle"],Vector2.zero);
            string[] bases={"WalkForward","WalkForwardLeft","StrafeLeft","WalkBackwardLeft","WalkBackward","WalkBackwardRight","StrafeRight","WalkForwardRight"};
            Vector2[] directions={Vector2.up,new Vector2(-1,1).normalized,Vector2.left,new Vector2(-1,-1).normalized,Vector2.down,new Vector2(1,-1).normalized,Vector2.right,new Vector2(1,1).normalized};
            float[] normal={0.818181818f,0.800000000f,0.290909091f,0.581818182f,0.581818182f,0.581818182f,0.290909091f,0.800000000f},brisk={1.071428571f,1.050000000f,0.407142857f,0.771428571f,0.771428571f,0.771428571f,0.407142857f,1.050000000f},power={1.375000000f,1.350000000f,0.550000000f,1.000000000f,1.000000000f,1.000000000f,0.550000000f,1.350000000f};
            for(int i=0;i<bases.Length;i++){tree.AddChild(clips[bases[i]],directions[i]*normal[i]);tree.AddChild(clips[bases[i]+"Brisk"],directions[i]*brisk[i]);tree.AddChild(clips[bases[i]+"Power"],directions[i]*power[i]);}
            loco.motion=tree;loco.speedParameterActive=true;loco.speedParameter="LocomotionRate";sm.defaultState=loco;
            var states=new Dictionary<string,AnimatorState>{{"Locomotion",loco}};
            foreach(var pair in clips){if(pair.Key=="A_TPose"||pair.Key=="Idle")continue;var state=sm.AddState(pair.Key);state.motion=pair.Value;states[pair.Key]=state;}
            states["Talk"].motion=clips[dark?"DoubleTalk":"Talk"];
            var talkIn=Transition(loco,states["Talk"],"Talking",true,.18f);talkIn.AddCondition(AnimatorConditionMode.Less,.08f,"Speed");Transition(states["Talk"],loco,"Talking",false,.18f);
            foreach(var name in new[]{"Interact","LookAround","Appear"})
            {
                var state=name=="Appear"?states["DoubleAppear"]:states[name];
                var entry=sm.AddAnyStateTransition(state);entry.hasExitTime=false;entry.duration=.18f;entry.hasFixedDuration=true;entry.canTransitionToSelf=false;entry.AddCondition(AnimatorConditionMode.If,0,name);
                var leave=Exit(state,loco,1,.18f);if(name=="LookAround")leave.AddCondition(AnimatorConditionMode.IfNot,0,"Talking");
            }
            var lookTalk=Transition(states["LookAround"],states["Talk"],"Talking",true,.22f);lookTalk.hasExitTime=true;lookTalk.exitTime=1;
            foreach(var name in new[]{"TalkGesture1","TalkGesture2","TalkGesture3"})Transition(states[name],loco,"Talking",false,.18f);
            foreach(var name in new[]{"WalkStart","WalkStopLeft","WalkStopRight","RunStopLeft","RunStopRight","TurnLeft","TurnRight","Turn45Left","Turn45Right","Turn90Left","Turn90Right"}){var e=Exit(states[name],loco,1,.10f);if(name=="WalkStart")e.offset=.5f;}
            Exit(states["JumpTakeoff"],states["JumpRise"],1,.025f);Exit(states["JumpApex"],states["FallLoop"],1,.035f);
            foreach(var name in new[]{"StartForward","StartForwardRight","StartRight","StartBackwardRight","StartBackward","StartBackwardLeft","StartLeft","StartForwardLeft","StartForwardPower","StartForwardRightPower","StartRightPower","StartBackwardRightPower","StartBackwardPower","StartBackwardLeftPower","StartLeftPower","StartForwardLeftPower"}){var t=Exit(states[name],loco,1,.10f);t.offset=name.StartsWith("StartLeft")?0:.5f;}
            foreach(var name in new[]{"Turn180Left","Turn180Right","PivotLeft","PivotRight","Nod","ShakeHead","Sigh","Startle","GazeUp","ReachDoor"})Exit(states[name],loco,1,.14f);
            Exit(states["JumpForwardTakeoff"],states["JumpRise"],1,.025f);
            foreach(var name in states.Keys.Where(n=>n.StartsWith("Land",StringComparison.Ordinal)&&!n.StartsWith("LandInto",StringComparison.Ordinal))){var exit=Exit(states[name],loco,name.StartsWith("LandTo")?1:.85f,.12f);if(name.StartsWith("LandTo"))exit.offset=.5125f;}
            foreach(var name in states.Keys.Where(n=>n.StartsWith("WalkStop",StringComparison.Ordinal)&&n!="WalkStopLeft"&&n!="WalkStopRight"))Exit(states[name],loco,1,.12f);
            foreach(var foot in new[]{"Left","Right"})
            {
                Exit(states["JumpFromStride"+foot],states["StrideRise"+foot],1,.08f);
                Exit(states["RunJump"+foot],states["RunStrideRise"+foot],1,.08f);
                Exit(states["RunStrideApex"+foot],states["RunStrideFall"+foot],1,.08f);
                Exit(states["StrideApex"+foot],states["StrideFall"+foot],1,.08f);
            }
            Exit(states["RunJump"],states["RunStrideRiseLeft"],1,.08f);
            Exit(states["ReachOut"],loco,1,.16f);Exit(states["HitReact"],states["ResolveIdle"],1,.16f);
            var resolved=states["ResolveIdle"].AddTransition(loco);resolved.hasExitTime=false;resolved.duration=.16f;resolved.hasFixedDuration=true;resolved.AddCondition(AnimatorConditionMode.Greater,.08f,"Speed");
            var run=sm.AddState("RunLocomotion");run.motion=clips["Run"];run.speedParameterActive=true;run.speedParameter="LocomotionRate";
            Exit(states["RunStart"],run,1,.12f).offset=.5f;
            var rush=sm.AddState("Rush");rush.motion=clips["DoubleRush"];rush.speedParameterActive=true;rush.speedParameter="LocomotionRate";
            var r=sm.AddAnyStateTransition(rush);r.hasExitTime=false;r.duration=.12f;r.hasFixedDuration=true;r.canTransitionToSelf=false;r.AddCondition(AnimatorConditionMode.If,0,"Rushing");Transition(rush,loco,"Rushing",false,.12f);
            EditorUtility.SetDirty(c);return c;
        }
        static AnimatorStateTransition Transition(AnimatorState from,AnimatorState to,string parameter,bool value,float seconds)
        {var t=from.AddTransition(to);t.hasExitTime=false;t.duration=seconds;t.hasFixedDuration=true;t.AddCondition(value?AnimatorConditionMode.If:AnimatorConditionMode.IfNot,0,parameter);return t;}
        static AnimatorStateTransition Exit(AnimatorState from,AnimatorState to,float exitTime,float seconds)
        {var t=from.AddTransition(to);t.hasExitTime=true;t.exitTime=exitTime;t.duration=seconds;t.hasFixedDuration=true;return t;}

        [Serializable] sealed class LayerWeight { public string[] names;public float[] values; }
        [Serializable] sealed class LayerProxy { public Vector3[] vertices;public int[] triangles;public LayerWeight[] weights;public float margin; }
        [Serializable] sealed class ShirtSurface { public int[] outer,hemRim;public Vector3[] positions; }
        static void ConfigureSurface(GameObject model,EchoSecondaryMotion secondary)
        {
            var text=AssetDatabase.LoadAssetAtPath<TextAsset>(Root+"/Editor/ShirtSurface.json");
            if(!text)throw new Exception("Missing shirt outer-surface metadata");
            var data=JsonUtility.FromJson<ShirtSurface>(text.text);
            var vertices=model.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(r=>r.name=="Shirt").sharedMesh.vertices;
            if(vertices.Length!=data.positions.Length)throw new Exception("Shirt vertex order changed; regenerate surface metadata");
            for(int i=0;i<vertices.Length;i++)if((vertices[i]-data.positions[i]).sqrMagnitude>1e-10f)throw new Exception("Shirt surface mapping mismatch at "+i);
            secondary.shirtOuterVertices=data.outer;secondary.shirtHemRimVertices=data.hemRim;
            var proxyText=AssetDatabase.LoadAssetAtPath<TextAsset>(Root+"/Editor/SkirtLayerProxy.json");
            if(!proxyText)throw new Exception("Missing skirt collision proxy");
            var proxy=JsonUtility.FromJson<LayerProxy>(proxyText.text);
            var skirt=model.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(r=>r.name=="Skirt");
            var indices=skirt.bones.Select((bone,i)=>new {bone.name,i}).ToDictionary(x=>x.name,x=>x.i);
            var weights=new BoneWeight[proxy.weights.Length];
            for(int i=0;i<weights.Length;i++)
            {
                var w=proxy.weights[i];int[] ids=new int[4];float[] values=new float[4];
                for(int j=0;j<w.names.Length;j++){ids[j]=indices[w.names[j]];values[j]=w.values[j];}
                weights[i]=new BoneWeight{boneIndex0=ids[0],boneIndex1=ids[1],boneIndex2=ids[2],boneIndex3=ids[3],weight0=values[0],weight1=values[1],weight2=values[2],weight3=values[3]};
            }
            secondary.skirtLayerVertices=proxy.vertices;secondary.skirtLayerTriangles=proxy.triangles;secondary.skirtLayerWeights=weights;secondary.skirtLayerMargin=proxy.margin;
        }
        static void SetupAuthoredPose(GameObject model,AnimationClip[] clips)
        {
            var originals=model.GetComponentsInChildren<Transform>(true);
            var parent=new GameObject("_EchoAuthoredPose").transform;parent.SetParent(model.transform,false);
            var map=new Dictionary<Transform,Transform>();
            foreach(var child in model.transform.Cast<Transform>().Where(t=>t!=parent).ToArray())ClonePoseNode(child,parent,map);
            foreach(var curve in SecondaryCurves.Values.SelectMany(list=>list))
                if(curve.binding.path.StartsWith("_EchoAuthoredPose/",StringComparison.Ordinal)&&!model.transform.Find(curve.binding.path))throw new Exception("Missing authored pose target: "+curve.binding.path);
            var names=PoseBoneNames.Where(n=>n!="LeftEye"&&n!="RightEye").ToArray();
            var targets=names.Select(n=>originals.Single(t=>t.name==n)).ToArray();
            model.AddComponent<EchoAuthoredPose>().Configure(targets,targets.Select(t=>map[t]).ToArray(),clips);
        }
        static void ClonePoseNode(Transform source,Transform parent,Dictionary<Transform,Transform> map)
        {
            if(!source.GetComponentsInChildren<Transform>(true).Any(t=>PoseBoneNames.Contains(t.name)))return;
            var clone=new GameObject("Pose_"+source.name).transform;clone.SetParent(parent,false);clone.localPosition=source.localPosition;clone.localRotation=source.localRotation;clone.localScale=source.localScale;map[source]=clone;
            foreach(Transform child in source)ClonePoseNode(child,clone,map);
        }
        static void BuildPrefab(bool dark,Dictionary<string,Material> materials,AnimatorController controller)
        {
            var root=new GameObject(dark?"TheDouble":"Protagonist");
            var model=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Model));
            model.name="Visual";model.transform.SetParent(root.transform,false);
            var animator=model.GetComponent<Animator>();if(!animator)animator=model.AddComponent<Animator>();
            SetupAuthoredPose(model,ClipNames.Select(n=>AssetDatabase.LoadAssetAtPath<AnimationClip>(Root+"/Animations/"+n+".anim")).ToArray());animator.runtimeAnimatorController=controller;ConfigureTurnCloth(model.AddComponent<EchoTurnFootIK>(),model);root.AddComponent<EchoArmPose>();animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            GameObject glasses=null,lenses=null;
            foreach(var r in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if(!materials.TryGetValue(r.gameObject.name,out var m))throw new Exception("Unexpected mesh "+r.gameObject.name);
                r.sharedMaterials=new[]{m};r.updateWhenOffscreen=true;r.quality=SkinQuality.Bone4;
                r.enabled=true;r.gameObject.SetActive(true);
                if(r.name=="Glasses")glasses=r.gameObject;if(r.name=="GlassesLens")lenses=r.gameObject;
            }
            root.AddComponent<EchoCharacterView>().Configure(animator,glasses,lenses,dark);
            var secondary=root.AddComponent<EchoSecondaryMotion>();secondary.enabled=true;secondary.layerGuard=false;
            ConfigureSurface(model,secondary);
            root.AddComponent<EchoEndingMotion>();root.AddComponent<EchoJumpMotion>();ConfigureFeet(model);root.AddComponent<EchoLocomotion>(); // opt in after removing legacy shader/bone sway
            string prefabPath=Root+"/Prefabs/"+root.name+".prefab";
            PrefabUtility.SaveAsPrefabAsset(root,prefabPath);UnityEngine.Object.DestroyImmediate(root);
            AssetDatabase.ImportAsset(prefabPath,ImportAssetOptions.ForceUpdate);
        }

        [MenuItem("Tools/Echo Characters/Create Preview Scene")]
        public static void CreateDemo()
        {
            var existing=UnityEngine.SceneManagement.SceneManager.GetSceneByPath(Root+"/Demo/CharacterPreview.unity");
            if(existing.IsValid()&&existing.isLoaded){Debug.Log("Character preview scene is already open.");return;}
            if (string.IsNullOrEmpty(UnityEngine.SceneManagement.SceneManager.GetActiveScene().path))
            { Debug.LogWarning("Save the current scene before creating the character preview scene."); return; }
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(scene);
            for(int i=0;i<2;i++)
            {
                string name=i==0?"Protagonist":"TheDouble";
                var go=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Prefabs/"+name+".prefab"));
                go.transform.position=new Vector3(i==0?-.65f:.65f,0,0);
            }
            var cam=new GameObject("Character Preview Camera").AddComponent<Camera>();cam.transform.position=new Vector3(0,.9f,3.2f);cam.transform.LookAt(new Vector3(0,.83f,0));cam.backgroundColor=new Color(.12f,.14f,.19f);cam.clearFlags=CameraClearFlags.SolidColor;cam.fieldOfView=35;
            var light=new GameObject("Key Light").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.2f;light.transform.rotation=Quaternion.Euler(35,150,0);
            RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.6f,.6f,.65f);
            EditorSceneManager.SaveScene(scene,Root+"/Demo/CharacterPreview.unity");
        }

        // Called only by the isolated validation project, never on project import.
        public static void BuildValidateExport()
        {
            try
            {
                Build();
                var activeScene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                if(string.IsNullOrEmpty(activeScene.path))EditorSceneManager.SaveScene(activeScene,"Assets/ValidationBootstrap.unity");
                CreateDemo();Validate();
                var output=Environment.GetEnvironmentVariable("ECHO_PACKAGE_OUTPUT");
                if(!string.IsNullOrEmpty(output))AssetDatabase.ExportPackage(new[]{Root+"/Models",Root+"/Animations",Root+"/Prefabs",Root+"/Runtime",Root+"/Editor"},output,ExportPackageOptions.Recurse);
                EditorApplication.Exit(0);
            }
            catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
        }

        public static void Validate()
        {
            var checks=new List<string>();
            foreach(var name in new[]{"Protagonist","TheDouble"})
            {
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Prefabs/"+name+".prefab");
                var go=UnityEngine.Object.Instantiate(prefab);var animator=go.GetComponentInChildren<Animator>();
                if(!animator||!animator.avatar||!animator.avatar.isValid||!animator.avatar.isHuman)throw new Exception(name+": Avatar invalid");
                checks.Add(name+": valid Humanoid Avatar");
                foreach(var bn in new[]{HumanBodyBones.Hips,HumanBodyBones.Head,HumanBodyBones.LeftHand,HumanBodyBones.RightHand,HumanBodyBones.LeftFoot,HumanBodyBones.RightFoot})
                    if(!animator.GetBoneTransform(bn))throw new Exception(name+": missing mapped bone "+bn);
                var skins=go.GetComponentsInChildren<SkinnedMeshRenderer>();
                if(skins.Length!=12)throw new Exception(name+": expected 12 skinned meshes, found "+skins.Length);
                foreach(var r in skins)
                {
                    if(!r.sharedMesh||r.bones.Length==0||r.bones.Any(b=>!b))throw new Exception(name+": invalid skin "+r.name);
                    if(r.sharedMaterials.Any(m=>!m||!m.shader||m.shader.name.Contains("Error")))throw new Exception(name+": missing material "+r.name);
                    var baked=new Mesh();r.BakeMesh(baked);
                    if(baked.vertices.Any(v=>!float.IsFinite(v.x)||!float.IsFinite(v.y)||!float.IsFinite(v.z)))throw new Exception("Non-finite skin vertex");
                    UnityEngine.Object.DestroyImmediate(baked);
                }
                checks.Add(name+": all 12 meshes skinned; all materials assigned");
                animator.Rebind();animator.Update(1f/30f);
                animator.Play("Locomotion",0,.25f);animator.Update(1f/30f);
                var idleHand=animator.GetBoneTransform(HumanBodyBones.RightHand);
                var idleShoulder=animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
                if(idleHand.position.y>idleShoulder.position.y-.15f)throw new Exception(name+": locomotion did not evaluate idle pose");
                checks.Add(name+": locomotion idle pose verified (arms lowered)");
                var hand=animator.GetBoneTransform(HumanBodyBones.RightHand);var rest=hand.position;
                animator.Play("Interact",0,.5f);animator.Update(1f/30f);
                float delta=Vector3.Distance(rest,hand.position);
                if(delta<.05f)throw new Exception(name+": animation did not move right hand "+delta);
                checks.Add(name+": Animator playback verified; Interact hand displacement "+delta.ToString("F3")+"m");
                foreach(string state in new[]{"Locomotion","Talk","LookAround","Rush","DoubleAppear"})
                {animator.Play(state,0,.35f);animator.Update(0);if(!animator.GetCurrentAnimatorStateInfo(0).IsName(state))throw new Exception("State not entered: "+state);}
                checks.Add(name+": all controller states evaluated");
                var view=go.GetComponent<EchoCharacterView>();view.SetGlassesVisible(false);
                if(go.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r=>r.name=="Glasses"||r.name=="GlassesLens").Any(r=>r.gameObject.activeSelf))throw new Exception("Glasses toggle failed");
                view.SetGlassesVisible(true);checks.Add(name+": glasses and lens toggle verified");
                UnityEngine.Object.DestroyImmediate(go);
            }
            foreach(var c in ClipNames){var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(Root+"/Animations/"+c+".anim");if(!clip||clip.length<=0||!clip.humanMotion)throw new Exception("Invalid humanoid clip "+c);checks.Add(c+": humanoid animation, "+clip.length.ToString("F3")+"s");}
            File.WriteAllText(Path.GetFullPath("UnityValidation.txt"),"Unity "+Application.unityVersion+"\nPASS\n"+string.Join("\n",checks));
            Debug.Log("ECHO_VALIDATION_PASS\n"+string.Join("\n",checks));
        }
    }
}
