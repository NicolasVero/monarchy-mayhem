    using System;
    using System.Linq;
    using System.Collections;
    using System.Collections.Generic;
    using UnityEngine;

    public class Weapon : MonoBehaviour {

        public int weaponID;
        public string weaponName;
        public string tier;
        public string family;
        public int id;
        public int attack;
        public float attackSpeed;
        public float knockback;
        public float range;
        public int regeneration;
        public float speed;

        void Start() {

            this.weaponID = Weapon.GetWeaponID(gameObject.name);
            TextAsset weaponsList = Resources.Load<TextAsset>("Data/WeaponsStats");

            if(weaponsList != null) {
                WeaponsStats weaponsStats = JsonUtility.FromJson<WeaponsStats>(weaponsList.text);
                WeaponStat weapon = Array.Find(weaponsStats.weapons, e => e.id == this.weaponID);

                this.id = weapon.id;
                this.weaponName = (GameController.GetSystemLanguageUpper() == "FR") ? weapon.name.fr : weapon.name.en;
                this.tier = weapon.tier;
                this.family = weapon.family;
                this.attack = weapon.attack;
                this.attackSpeed = weapon.attackSpeed;
                this.knockback = weapon.knockback;
                this.range = weapon.range;
                this.regeneration = weapon.regeneration;
                this.speed = weapon.speed;

                Transform lightTransform = transform.Find("weapon_light");
                Light lightComponent = lightTransform.GetComponent<Light>();
                lightComponent.color = Color.red; 


                Dictionary<string, string> tierColors = new Dictionary<string, string> {
                    { "S", "#FF7C00" },         
                    { "A", "#8E00FF" },
                    { "B", "#0046FF" },
                    { "C", "#77FF00" },
                    { "D", "#FFFFFF" }
                };

                string hexColor;
                tierColors.TryGetValue(tier, out hexColor);
                lightComponent.color = GetColorFromHexadecimal(hexColor);

                Destroy(this.gameObject, 120f);
            }       
        }

        

        private Color GetColorFromHexadecimal(string hexColor) {
            Color color;
            ColorUtility.TryParseHtmlString(hexColor, out color);
            return color;
        }

        private static int GetWeaponID(string name) {
            name = name.Replace("(Clone)", "");
            return Convert.ToInt32(name.Split('_')[1]);
        }
    }
