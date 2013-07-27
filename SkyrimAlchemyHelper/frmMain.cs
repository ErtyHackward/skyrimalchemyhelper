using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Xml.Serialization;
using SkyrimAlchemyHelper.Properties;

namespace SkyrimAlchemyHelper
{
    public partial class frmMain : Form
    {
        private AlchemyBase _base;

        public frmMain()
        {
            InitializeComponent();

            XmlSerializer xml;

            try
            {
                 xml = new XmlSerializer(typeof(AlchemyBase));

            }
            catch (Exception x)
            {
                MessageBox.Show(
                    "Ошибка сериализации, невозможно создать сериализатор. Попробуйте запустить программу с правами администратора.",
                    "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Application.Exit();
                return;
            }
            
            try
            {
                using (var fs = new StringReader(Resources.alchemy))
                {
                    _base = (AlchemyBase)xml.Deserialize(fs);
                }
            }
            catch (Exception x)
            {
                MessageBox.Show(
                    "Не удается прочитать базу данных. Возможно данные повреждены, переустановите программу.",
                    "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Application.Exit();
                return;
            }

            foreach (var item in _base.Items)
            {
                comboBox1.Items.Add(item);
                checkedListBox1.Items.Add(item);
            }

            foreach (var effect in _base.Effects)
            {
                comboBox2.Items.Add(effect);
            }

            comboBox1.SelectedIndex = 0;

        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            var item = (Item)comboBox1.SelectedItem;
            
            var e1 = _base.Effects[item.Effects[0]];
            var e2 = _base.Effects[item.Effects[1]];
            var e3 = _base.Effects[item.Effects[2]];
            var e4 = _base.Effects[item.Effects[3]];

            linkLabel1.Text = string.Format("{0} ({1})", e1.Name, e1.Value);
            linkLabel2.Text = string.Format("{0} ({1})", e2.Name, e2.Value);
            linkLabel3.Text = string.Format("{0} ({1})", e3.Name, e3.Value);
            linkLabel4.Text = string.Format("{0} ({1})", e4.Name, e4.Value);

            linkLabel1.Tag = e1;
            linkLabel2.Tag = e2;
            linkLabel3.Tag = e3;
            linkLabel4.Tag = e4;


            listBox2.Items.Clear();

            var list = new List<KeyValuePair<Item, string[]>>();

            foreach (var i in _base.Items)
            {
                if (i == item)
                    continue;
                var hashMap = new HashSet<int>();

                var matches = i.Effects.Where(id => item.Effects.Contains(id)).Select(eid => _base.Effects[eid].Name).ToArray();

                if (matches.Length > 0)
                {
                    list.Add(new KeyValuePair<Item, string[]>(i, matches));
                }               
            }

            list.Sort((i1, i2) => i2.Value.Length.CompareTo(i1.Value.Length));

            foreach (var keyValuePair in list)
            {
                listBox2.Items.Add(string.Format("{0} + {1} ({2})", item.Name, keyValuePair.Key.Name, string.Join(", ", keyValuePair.Value)));
            }

        }

        private void comboBox2_SelectedIndexChanged(object sender, EventArgs e)
        {
            var effect = (Effect)comboBox2.SelectedItem;

            listBox1.Items.Clear();

            foreach (var item in _base.Items)
            {
                if (item.Effects.Contains(effect.Id))
                    listBox1.Items.Add(item);
            }
        }

        private void linkLabel3_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            comboBox2.SelectedItem = ( (LinkLabel)sender ).Tag;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            listBox4.BeginUpdate();
            listBox4.Items.Clear();
            foreach (var recipe in FindBestIn(checkedListBox1.CheckedItems.Cast<Item>()).Take(1000))
            {
                listBox4.Items.Add(recipe);
            }
            listBox4.EndUpdate();
        }

        private IEnumerable<Recipe> FindBestIn(IEnumerable<Item> items)
        {
            var recipes = new List<Recipe>();
            var hashSet = new Hashtable();
            // 2 elements

            var enumerable = items as Item[] ?? items.ToArray();

            foreach (var item1 in enumerable)
            {
                foreach (var item2 in enumerable)
                {
                    if (item1 == item2)
                        continue;
                    if (Recipe.CanMix(item1, item2))
                    {
                        var recipe = new Recipe { Items = new[] { item1, item2 } };
                        if (!hashSet.Contains(recipe.GetHashCode()))
                        {
                            hashSet.Add(recipe.GetHashCode(), recipe);
                            recipes.Add(recipe);
                        }
                    }
                }
            }

            // 3 elements

            foreach (var item1 in enumerable)
            {
                foreach (var item2 in enumerable)
                {
                    if (item1 == item2)
                        continue;
                    foreach (var item3 in enumerable)
                    {
                        if (item1 == item3 || item2 == item3)
                            continue;
                        if (Recipe.CanMix(item1, item2, item3))
                        {
                            var recipe = new Recipe { Items = new[] { item1, item2, item3 } };

                            if (!hashSet.Contains(recipe.GetHashCode()))
                            {
                                hashSet.Add(recipe.GetHashCode(), recipe);
                                recipes.Add(recipe);
                            }
                        }
                    }
                }
            }

            foreach (var recipe in recipes)
            {
                recipe.Calculate(_base);
            }

            recipes.Sort((r1, r2) => r2.Value.CompareTo(r1.Value));

            return recipes;
        }

        private void button2_Click(object sender, EventArgs e)
        {
            bool check = checkedListBox1.CheckedItems.Count == 0;

            for (int i = 0; i < checkedListBox1.Items.Count; i++)
            {
                checkedListBox1.SetItemChecked(i, check);
            }
        }

        private void linkLabel5_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Process.Start("http://skyrim-alchemy.ru");
        }

        private void linkLabel6_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Process.Start("http://ru.elderscrolls.wikia.com/wiki/%D0%90%D0%BB%D1%85%D0%B8%D0%BC%D0%B8%D1%8F_(Skyrim)");
        }
    }

    public class Recipe
    {
        private int _hashCode;

        public Item[] Items;
        public int Value;

        public static bool CanMix(Item item1, Item item2)
        {
            return item1.Effects.Any(e => item2.Effects.Contains(e));
        }

        public static bool CanMix(Item item1, Item item2, Item item3)
        {
            int mixes = 0;

            if (CanMix(item1, item2))
                mixes++;

            if (CanMix(item1, item3))
                mixes++;

            if (CanMix(item2, item3))
                mixes++;

            return mixes > 1;
        }

        public void Calculate(AlchemyBase db)
        {
            int[] effects;

            if (Items.Length == 2)
            {
                effects = Items[0].Effects.Where(e => Items[1].Effects.Contains(e)).ToArray();
            }
            else
            {
                var e1 = Items[0].Effects.Where(e => Items[1].Effects.Contains(e)).ToArray();
                var e2 = Items[0].Effects.Where(e => Items[2].Effects.Contains(e)).ToArray();
                var e3 = Items[1].Effects.Where(e => Items[2].Effects.Contains(e)).ToArray();

                effects = e1.Concat(e2.Concat(e3)).Distinct().ToArray();
            }

            Value = effects.Select(e => db.Effects[e].Value).Sum();
        }

        public override int GetHashCode()
        {
            if (_hashCode != 0)
                return _hashCode;

            var arr = Items.Select(i => i.Name).ToList();
            arr.Sort();
            _hashCode = string.Join("", arr).GetHashCode();

            return _hashCode;
        }

        public override string ToString()
        {
            return string.Format("{0} value {1}", string.Join(" + ", Items.Select(i => i.Name)), Value);
        }
    }

    [Serializable]
    public class Item
    {
        public string Name;
        public int[] Effects;

        public override string ToString()
        {
            return Name;
        }
    }

    [Serializable]
    public class Effect
    {
        public int Id;
        public string Name;
        public int Value;

        public override string ToString()
        {
#if DEBUG
            return string.Format("{0} ({1})",Name, Id);
#else
            return Name;
#endif
        }
    }

    [Serializable]
    public class AlchemyBase
    {
        public List<Effect> Effects;
        public List<Item> Items;
    }
}
