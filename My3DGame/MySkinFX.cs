﻿using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace My3DGame {

    class DirectionLight {
        public Vector3 direction;
        public Vector3 diffuseColor;
        public Vector3 specularColor;
        public bool enabled;
    }

    class MySkinFX {

        public const int MAX_BONES          = 180; // limit was 72 - boosted ( same in MySkinEffect.fx )
        public const int WEIGHTS_PER_VERTEX = 4; 

        // referenced:
        public Camera cam;

        public Effect fx;
        public Texture2D diffuseTex, default_tex;
        public Vector4   diffuseCol  = Vector4.One;
        public Vector3   emissiveCol = Vector3.Zero;        
        public Vector3   specularCol = Color.LightYellow.ToVector3();
        public float     specularPow = 32f;
        public Vector3   ambientCol  = Vector3.Zero;
        public bool      preferPerPixelLighting = true;
        public bool      oneLight    = false;
        public bool      fogEnabled  = false;               
        public DirectionLight[] lights;         // lights: key, fill, back 
        public float     alpha       = 1f;
        public float     fogStart    = 0f;
        public float     fogEnd      = 1f;
        public Matrix    world       = Matrix.Identity;
        public Matrix    worldView   = Matrix.Identity;


        //------------------
        // C O N S T R U C T
        //------------------
        public MySkinFX(ContentManager Content, Camera Cam, string fx_filename, bool enableAllLights = true, bool enableFog = false)
        {
            lights = new DirectionLight[3];
            for (int i = 0; i < 3; i++) lights[i] = new DirectionLight();
            cam         = Cam;
            fx          = Content.Load<Effect>(fx_filename);            
            default_tex = Content.Load<Texture2D>("default_texture");
            Matrix[] identityBones = new Matrix[MAX_BONES];
            for (int i = 0; i < MAX_BONES; i++) {
                identityBones[i] = Matrix.Identity;
            }
            SetBoneTransforms(identityBones);
            fx.Parameters["DiffuseTexture"].SetValue(default_tex);
            fx.Parameters["DiffuseColor"].SetValue(diffuseCol);
            fx.Parameters["EmissiveColor"].SetValue(emissiveCol);
            fx.Parameters["SpecularColor"].SetValue(specularCol);
            fx.Parameters["SpecularPower"].SetValue(specularPow);

            ToggleDefaultLighting(enableAllLights);
            if (enableFog) ToggleFog();
        }
        #region TO DO: PROPER CLONE
        //// C O N S T R U C T   (copy from pre-existing effects) 
        //public MySkinFX(MySkinFX clone, SkinnedEffect effect)
        //{
        //    cam         = clone.cam;
        //    fx          = clone.fx;
        //    default_tex = clone.default_tex;
        //    diffuseTex  = effect.Texture;        // USE EFFECT TEXTURE (could copy over other things too)     
        //    diffuseCol  = clone.diffuseCol;
        //    emissiveCol = clone.emissiveCol;
        //    specularCol = clone.specularCol;
        //    specularPow = clone.specularPow;
        //    ambientCol  = clone.ambientCol;
        //    preferPerPixelLighting = clone.preferPerPixelLighting;
        //    oneLight    = clone.oneLight;
        //    fogEnabled  = clone.fogEnabled;
        //    lights      = clone.lights;     // just refer to the first one (assuming they all use the same lighting [for now] )
        //    alpha       = clone.alpha;
        //    fogStart    = clone.fogStart;
        //    fogEnd      = clone.fogEnd;
        //    world       = clone.world;
        //    worldView   = clone.worldView;        
        //}   
        #endregion



        //--------------------------------------
        // S E T   B O N E   T R A N S F O R M S 
        //--------------------------------------
        /// <summary> Sets an array of skinning bone transform matrices. </summary>
        public void SetBoneTransforms(Matrix[] boneTransforms) {
            if ((boneTransforms == null) || (boneTransforms.Length == 0)) throw new ArgumentNullException("boneTransforms");
            if (boneTransforms.Length > MAX_BONES) throw new ArgumentException();
            fx.Parameters["Bones"].SetValue(boneTransforms);            
        }



        //----------------------------------------------
        // E N A B L E   D E F A U L T   L I G H T I N G
        //----------------------------------------------
        public void ToggleDefaultLighting(bool all_lights)
        {
            if (all_lights) oneLight = false; else oneLight = true;
            float u = cam.up.Y;     // I assume up is -Y or +Y
            ambientCol              = new Vector3(0.05333332f, 0.09882354f, 0.1819608f);
            lights[0].direction     = new Vector3(-0.5265408f, -0.5735765f * u, -0.6275069f); // Key light.
            lights[0].diffuseColor  = new Vector3(1, 0.9607844f, 0.8078432f);
            lights[0].specularColor = new Vector3(1, 0.9607844f, 0.8078432f);
            lights[0].enabled       = true;
            fx.Parameters["DirLight0Direction"].SetValue(lights[0].direction);
            fx.Parameters["DirLight0DiffuseColor"].SetValue(lights[0].diffuseColor);
            fx.Parameters["DirLight0SpecularColor"].SetValue(lights[0].specularColor);
            if (all_lights) {                
                lights[1].direction     = new Vector3(0.7198464f, 0.3420201f * u, 0.6040227f);    // Fill light
                lights[1].diffuseColor  = new Vector3(0.9647059f, 0.7607844f, 0.4078432f);
                lights[1].specularColor = Vector3.Zero;
                lights[1].enabled       = true;
                lights[2].direction     = new Vector3(0.4545195f, -0.7660444f * u, 0.4545195f);   // Back light
                lights[2].diffuseColor  = new Vector3(0.3231373f, 0.3607844f, 0.3937255f);
                lights[2].specularColor = new Vector3(0.3231373f, 0.3607844f, 0.3937255f);
                lights[2].enabled       = true;                
                fx.Parameters["DirLight1Direction"].SetValue(lights[1].direction);
                fx.Parameters["DirLight1DiffuseColor"].SetValue(lights[1].diffuseColor);
                fx.Parameters["DirLight1SpecularColor"].SetValue(lights[1].specularColor);
                fx.Parameters["DirLight2Direction"].SetValue(lights[2].direction);
                fx.Parameters["DirLight2DiffuseColor"].SetValue(lights[2].diffuseColor);
                fx.Parameters["DirLight2SpecularColor"].SetValue(lights[2].specularColor);
            } 
        }



        // S E T   D I R E C T I O N A L   L I G H T 
        public void SetDirectionalLight(int index, Vector3 direction, Color diffuse_color, Color specular_color)
        {
            if (index >= 3) return;
            lights[index].direction = direction;
            lights[index].diffuseColor = diffuse_color.ToVector3();
            lights[index].specularColor = specular_color.ToVector3();
            lights[index].enabled = true;
            switch (index) {
                case 0: fx.Parameters["DirLight0Direction"].SetValue(lights[0].direction);
                        fx.Parameters["DirLight0DiffuseColor"].SetValue(lights[0].diffuseColor);
                        fx.Parameters["DirLight0SpecularColor"].SetValue(lights[0].specularColor); break;
                case 1: fx.Parameters["DirLight1Direction"].SetValue(lights[1].direction);
                        fx.Parameters["DirLight1DiffuseColor"].SetValue(lights[1].diffuseColor);
                        fx.Parameters["DirLight1SpecularColor"].SetValue(lights[1].specularColor); break;
                case 2: fx.Parameters["DirLight2Direction"].SetValue(lights[2].direction);
                        fx.Parameters["DirLight2DiffuseColor"].SetValue(lights[2].diffuseColor);
                        fx.Parameters["DirLight2SpecularColor"].SetValue(lights[2].specularColor); break;
            }
        }

    
        
        public void SetFogStart(float fog_start)  {  fogEnabled = false;  fogStart = fog_start;  ToggleFog();  }
        public void SetFogEnd(float fog_end)      {  fogEnabled = false;  fogEnd   = fog_end;    ToggleFog(); }
        public void SetFogColor(Color fog_color)  { fx.Parameters["FogColor"].SetValue(fog_color.ToVector3()); }

        // T O G G L E   F O G 
        public void ToggleFog()
        {
            if (!fogEnabled) {                                                
                if (fogStart == fogEnd) {
                    fx.Parameters["FogVector"].SetValue(new Vector4(0,0,0,1)); // 100% fog                    
                } else {
                    // We want to transform vertex positions into view space, take the resulting Z value, then scale and offset according to the fog start/end distances.
                    // Because we only care about the Z component, the shader can do all this with a single dot product, using only the Z row of the world+view matrix.
                    float scale = 1f / (fogStart - fogEnd);
                    Vector4 fogVector = new Vector4();
                    fogVector.X = worldView.M13 * scale;
                    fogVector.Y = worldView.M23 * scale;
                    fogVector.Z = worldView.M33 * scale;
                    fogVector.W = (worldView.M43 + fogStart) * scale;
                    fx.Parameters["FogVector"].SetValue(fogVector);
                    fogEnabled = true;
                }
            } else { fx.Parameters["FogVector"].SetValue(Vector4.Zero); fogEnabled = false; }
        }



        //------------------------------
        // S E T   D R A W   P A R A M S (use just before drawing)
        //------------------------------
        public void SetDrawParams(Camera cam, Texture2D tex)
        {
            Matrix.Multiply(ref world, ref cam.view, out worldView); // (used with fog)
            Matrix worldTranspose, worldInverseTranspose;
            Matrix.Invert(ref world, out worldTranspose);
            Matrix.Transpose(ref worldTranspose, out worldInverseTranspose);
            Vector4 diffuse  = new Vector4();
            Vector3 emissive = new Vector3();
            diffuse.X = diffuseCol.X * alpha;
            diffuse.Y = diffuseCol.Y * alpha;
            diffuse.Z = diffuseCol.Z * alpha;
            diffuse.W = alpha;
            emissive.X = (emissiveCol.X + ambientCol.X * diffuseCol.X) * alpha;
            emissive.Y = (emissiveCol.Y + ambientCol.Y * diffuseCol.Y) * alpha;
            emissive.Z = (emissiveCol.Z + ambientCol.Z * diffuseCol.Z) * alpha;
            fx.Parameters["World"].SetValue(world);            
            fx.Parameters["WorldViewProj"].SetValue(world * cam.view_proj);
            fx.Parameters["CamPos"].SetValue(cam.pos);
            if (tex != null) fx.Parameters["DiffuseTexture"].SetValue(tex);   else fx.Parameters["DiffuseTexture"].SetValue(default_tex);            
            fx.Parameters["WorldInverseTranspose"].SetValue(worldInverseTranspose);            
            fx.Parameters["DiffuseColor"].SetValue(diffuse);
            fx.Parameters["EmissiveColor"].SetValue(emissive);
            if (fogEnabled) {
                if (preferPerPixelLighting) fx.CurrentTechnique = fx.Techniques["Skin_PerPixel_Lit_Fog"]; 
                else if (oneLight)          fx.CurrentTechnique = fx.Techniques["Skin_Vertex_1Light_Fog"];
                else                        fx.CurrentTechnique = fx.Techniques["Skin_Vertex_Lights_Fog"];
            } else {
                if (preferPerPixelLighting) fx.CurrentTechnique = fx.Techniques["Skin_PerPixel_Lit"];
                else if (oneLight)          fx.CurrentTechnique = fx.Techniques["Skin_Vertex_1Light"];
                else                        fx.CurrentTechnique = fx.Techniques["Skin_Vertex_Lights"];
            }
            fx.CurrentTechnique.Passes[0].Apply();
        }
        
        public void SetDiffuseCol(Vector4 diffuse)   { diffuseCol = diffuse; }
        public void SetEmissiveCol(Vector3 emissive) { emissiveCol = emissive; }
        public void SetSpecularCol(Vector3 specular) { specularCol = specular; fx.Parameters["SpecularColor"].SetValue(specularCol); }
        public void SetSpecularPow(float power)      { specularPow = power;    fx.Parameters["SpecularPower"].SetValue(power); }        
        public void SetShineAmplify(float amp)       { fx.Parameters["shine_amplify"].SetValue(amp); }
    }
}

