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
        static readonly string[] ClipNames = {"Idle","Walk","Run","Talk","LookAround","Interact","DoubleIdle","DoubleTalk","DoubleRush","DoubleAppear"};
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
            imp.importAnimation = true;
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
                if(clip.name.StartsWith("__preview__"))continue;
                string clean=ClipNames.FirstOrDefault(n=>clip.name==n||clip.name.EndsWith("|"+n,StringComparison.Ordinal));
                if(clean==null)continue;
                var curves=new List<ExtraCurve>();
                foreach(var binding in AnimationUtility.GetCurveBindings(clip))
                    if(binding.type==typeof(Transform)&&IsSecondaryPath(binding.path))
                    {
                        var source=AnimationUtility.GetEditorCurve(clip,binding);
                        curves.Add(new ExtraCurve{binding=binding,curve=new AnimationCurve(source.keys){preWrapMode=source.preWrapMode,postWrapMode=source.postWrapMode}});
                    }
                SecondaryCurves[clean]=curves;
            }
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
                    upperArmTwist=.5f, lowerArmTwist=.5f, upperLegTwist=.5f, lowerLegTwist=.5f,
                    armStretch=0f, legStretch=0f, feetSpacing=0, hasTranslationDoF=false
                };
                imp.animationType = ModelImporterAnimationType.Human;
                imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                imp.humanDescription = hd;
                var defaults = imp.defaultClipAnimations;
                foreach (var clip in defaults)
                {
                    string clean = ClipNames.FirstOrDefault(n => clip.name == n || clip.name.EndsWith("|" + n) || clip.name.EndsWith("_" + n));
                    if (clean == null) clean = ClipNames.FirstOrDefault(n => clip.takeName.EndsWith("|" + n) || clip.takeName == n);
                    if (clean != null) clip.name=clean;
                    clip.loopTime = clean != "Interact" && clean != "DoubleAppear";
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
                if(SecondaryCurves.TryGetValue(name,out var extra))
                    foreach(var curve in extra)AnimationUtility.SetEditorCurve(target,curve.binding,curve.curve);
                target.name=name;EditorUtility.SetDirty(target);result[name]=target;
            }
            return result;
        }

        static Dictionary<string,Material> BuildMaterials(bool dark)
        {
            var result=new Dictionary<string,Material>();
            string[] meshes={"Body","Shirt","Shoes","Skirt","Hair","HairBase","Glasses","GlassesLens","Eyes","Ribbon","Earrings","Buttons"};
            string[] textures={"Skin","Shirt","Shoe","Skirt","Hair","Hair","Glasses",null,"Eye","Ribbon","Earring","Button"};
            var shader=Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (!shader) throw new Exception("No supported Lit shader available.");
            bool urp=shader.name.Contains("Universal");
            for(int i=0;i<meshes.Length;i++)
            {
                string key=meshes[i];string path=Root+"/Materials/"+(dark?"Double_":"Hero_")+key+".mat";
                var m=AssetDatabase.LoadAssetAtPath<Material>(path);
                if(!m){m=new Material(shader);AssetDatabase.CreateAsset(m,path);}else m.shader=shader;
                bool lens=key=="GlassesLens", eye=dark&&key=="Eyes";
                if(lens&&urp)m.shader=Shader.Find("Universal Render Pipeline/Unlit")??shader;
                Color color=lens?new Color(.78f,.90f,1,.045f):eye?new Color(1,.005f,.008f):dark?new Color(.018f,.022f,.038f):Color.white;
                var tex=dark||lens?null:AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Textures/"+textures[i]+".png");
                m.SetColor(urp?"_BaseColor":"_Color",color);m.SetTexture(urp?"_BaseMap":"_MainTex",tex);
                if(m.HasProperty("_Metallic"))m.SetFloat("_Metallic",!dark&&(key=="Glasses"||key=="Earrings"||key=="Buttons")?.35f:0);
                if(m.HasProperty(urp?"_Smoothness":"_Glossiness"))m.SetFloat(urp?"_Smoothness":"_Glossiness",lens?.2f:.25f);
                if(urp) {m.SetFloat("_Cull",0);m.SetFloat("_Surface",lens?1:0);m.SetFloat("_Blend",0);}
                if(lens)
                {
                    if(m.HasProperty("_BlendModePreserveSpecular"))m.SetFloat("_BlendModePreserveSpecular",0);
                    m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                    if(urp)m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");else {m.SetFloat("_Mode",3);m.EnableKeyword("_ALPHAPREMULTIPLY_ON");}
                    m.SetInt("_SrcBlend",(int)BlendMode.SrcAlpha);m.SetInt("_DstBlend",(int)BlendMode.OneMinusSrcAlpha);m.SetInt("_ZWrite",0);m.renderQueue=3000;
                    m.SetShaderPassEnabled("ShadowCaster",false);
                }
                else {m.SetInt("_ZWrite",1);m.SetInt("_SrcBlend",1);m.SetInt("_DstBlend",0);m.renderQueue=-1;}
                if(eye){m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",new Color(3,.005f,.008f));m.globalIlluminationFlags=MaterialGlobalIlluminationFlags.RealtimeEmissive;}
                else {m.DisableKeyword("_EMISSION");if(m.HasProperty("_EmissionColor"))m.SetColor("_EmissionColor",Color.black);}
                EditorUtility.SetDirty(m);result[key]=m;
            }
            return result;
        }

        static AnimatorController BuildController(bool dark, Dictionary<string,AnimationClip> clips)
        {
            string path=Root+"/Animations/"+(dark?"TheDouble":"Protagonist")+".controller";
            var c=AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if(!c)c=AnimatorController.CreateAnimatorControllerAtPath(path);
            else
            {
                c.layers=Array.Empty<AnimatorControllerLayer>();c.parameters=Array.Empty<AnimatorControllerParameter>();
                foreach(var sub in AssetDatabase.LoadAllAssetsAtPath(path))if(sub&&sub!=c)UnityEngine.Object.DestroyImmediate(sub,true);
                var machine=new AnimatorStateMachine{name="Base Layer"};AssetDatabase.AddObjectToAsset(machine,c);
                c.layers=new[]{new AnimatorControllerLayer{name="Base Layer",defaultWeight=1,stateMachine=machine}};
            }
            c.AddParameter("Speed",AnimatorControllerParameterType.Float);c.AddParameter("Talking",AnimatorControllerParameterType.Bool);c.AddParameter("Rushing",AnimatorControllerParameterType.Bool);
            foreach(var t in new[]{"Interact","LookAround","Appear"})c.AddParameter(t,AnimatorControllerParameterType.Trigger);
            var sm=c.layers[0].stateMachine;
            var loco=sm.AddState("Locomotion");var tree=new BlendTree {name="Locomotion",blendParameter="Speed",useAutomaticThresholds=false};AssetDatabase.AddObjectToAsset(tree,c);
            tree.AddChild(clips[dark?"DoubleIdle":"Idle"],0);tree.AddChild(clips["Walk"],.5f);tree.AddChild(clips["Run"],1);loco.motion=tree;sm.defaultState=loco;
            var talk=sm.AddState("Talk");talk.motion=clips[dark?"DoubleTalk":"Talk"];
            Transition(loco,talk,"Talking",true);Transition(talk,loco,"Talking",false);
            foreach(var name in new[]{"Interact","LookAround","Appear"})
            {
                var s=sm.AddState(name);s.motion=clips[name=="Appear"?"DoubleAppear":name];
                var entry=sm.AddAnyStateTransition(s);entry.hasExitTime=false;entry.duration=.12f;entry.canTransitionToSelf=false;entry.AddCondition(AnimatorConditionMode.If,0,name);
                var exit=s.AddTransition(loco);exit.hasExitTime=true;exit.exitTime=.95f;exit.duration=.15f;
            }
            var rush=sm.AddState("Rush");rush.motion=clips["DoubleRush"];
            var rt=sm.AddAnyStateTransition(rush);rt.hasExitTime=false;rt.canTransitionToSelf=false;rt.duration=.12f;rt.AddCondition(AnimatorConditionMode.If,0,"Rushing");
            Transition(rush,loco,"Rushing",false);EditorUtility.SetDirty(c);return c;
        }
        static void Transition(AnimatorState from,AnimatorState to,string parameter,bool value)
        {var t=from.AddTransition(to);t.hasExitTime=false;t.duration=.16f;t.AddCondition(value?AnimatorConditionMode.If:AnimatorConditionMode.IfNot,0,parameter);}

        static void BuildPrefab(bool dark,Dictionary<string,Material> materials,AnimatorController controller)
        {
            var root=new GameObject(dark?"TheDouble":"Protagonist");
            var model=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Model));
            model.name="Visual";model.transform.SetParent(root.transform,false);
            var animator=model.GetComponent<Animator>();if(!animator)animator=model.AddComponent<Animator>();
            animator.runtimeAnimatorController=controller;animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            GameObject glasses=null,lenses=null;
            foreach(var r in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if(!materials.TryGetValue(r.gameObject.name,out var m))throw new Exception("Unexpected mesh "+r.gameObject.name);
                r.sharedMaterials=new[]{m};r.updateWhenOffscreen=true;r.quality=SkinQuality.Bone4;
                r.enabled=true;r.gameObject.SetActive(true);
                if(r.name=="Glasses")glasses=r.gameObject;if(r.name=="GlassesLens")lenses=r.gameObject;
            }
            root.AddComponent<EchoCharacterView>().Configure(animator,glasses,lenses,dark);
            root.AddComponent<EchoSecondaryMotion>().enabled=false; // opt in after removing legacy shader/bone sway
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
                if(!string.IsNullOrEmpty(output))AssetDatabase.ExportPackage(Root,output,ExportPackageOptions.Recurse);
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
                foreach(string state in new[]{"Locomotion","Talk","LookAround","Rush","Appear"})
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
