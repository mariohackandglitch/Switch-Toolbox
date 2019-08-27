﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Toolbox;
using System.Windows.Forms;
using Toolbox.Library;
using Toolbox.Library.IO;
using FirstPlugin.Forms;
using Syroot.Maths;
using SharpYaml.Serialization;

namespace FirstPlugin
{
    public class BFLYT : IFileFormat, IEditor<LayoutEditor>, IConvertableTextFormat
    {
        public FileType FileType { get; set; } = FileType.Layout;

        public bool CanSave { get; set; }
        public string[] Description { get; set; } = new string[] { "Cafe Layout (GUI)" };
        public string[] Extension { get; set; } = new string[] { "*.bflyt" };
        public string FileName { get; set; }
        public string FilePath { get; set; }
        public IFileInfo IFileInfo { get; set; }

        public bool Identify(System.IO.Stream stream)
        {
            using (var reader = new Toolbox.Library.IO.FileReader(stream, true))
            {
                return reader.CheckSignature(4, "FLYT");
            }
        }

        public Type[] Types
        {
            get
            {
                List<Type> types = new List<Type>();
                return types.ToArray();
            }
        }

        #region Text Converter Interface
        public TextFileType TextFileType => TextFileType.Xml;
        public bool CanConvertBack => true;

        public string ConvertToString()
        {
            var serializerSettings = new SerializerSettings()
            {
                //  EmitTags = false
            };

            serializerSettings.DefaultStyle = SharpYaml.YamlStyle.Any;
            serializerSettings.ComparerForKeySorting = null;
            serializerSettings.RegisterTagMapping("Header", typeof(Header));

            var serializer = new Serializer(serializerSettings);
            string yaml = serializer.Serialize(header, typeof(Header));
            return yaml;
        }

        public void ConvertFromString(string text)
        {
        }

        #endregion

        public LayoutEditor OpenForm()
        {
            LayoutEditor editor = new LayoutEditor();
            editor.Dock = DockStyle.Fill;
            editor.LoadBflyt(header, FileName);
            return editor;
        }

        public void FillEditor(UserControl control) {
            ((LayoutEditor)control).LoadBflyt(header, FileName);
        }

        private Header header;
        public void Load(System.IO.Stream stream)
        {
            CanSave = true;

            header = new Header();
            header.Read(new FileReader(stream), FileName);
        }

        public void Unload()
        {

        }

        public void Save(System.IO.Stream stream) {
            header.Write(new FileWriter(stream));
        }

        //Thanks to SwitchThemes for flags, and enums
        //https://github.com/FuryBaguette/SwitchLayoutEditor/tree/master/SwitchThemesCommon
        public class Header
        {
            public string FileName { get; set; }

            private const string Magic = "FLYT";

            private ushort ByteOrderMark;
            private ushort HeaderSize;

            internal uint Version;

            public string VersionFull
            {
                get
                {
                    var major = Version >> 24;
                    var minor = Version >> 16 & 0xFF;
                    var micro = Version >> 8 & 0xFF;
                    var micro2 = Version & 0xFF;
                    return $"{major} {minor} {micro} {micro2}";
                }
            }



            public LYT1 LayoutInfo { get; set; }
            public TXL1 TextureList { get; set; }
            public MAT1 MaterialList { get; set; }
            public FNL1 FontList { get; set; }

            //   private List<SectionCommon> Sections;

            public PAN1 RootPane { get; set; }
            public GRP1 RootGroup { get; set; }

          //  public List<PAN1> Panes = new List<PAN1>();

            public void Read(FileReader reader, string fileName)
            {
                FileName = fileName;

                reader.SetByteOrder(true);
                reader.ReadSignature(4, Magic);
                ByteOrderMark = reader.ReadUInt16();
                reader.CheckByteOrderMark(ByteOrderMark);
                HeaderSize = reader.ReadUInt16();
                Version = reader.ReadUInt32();
                uint FileSize = reader.ReadUInt32();
                ushort sectionCount = reader.ReadUInt16();
                reader.ReadUInt16(); //Padding

                bool setRoot = false;
                bool setGroupRoot = false;

                BasePane currentPane = null;
                BasePane parentPane = null;

                reader.SeekBegin(HeaderSize);
                for (int i = 0; i < sectionCount; i++)
                {
                    long pos = reader.Position;

                    string Signature = reader.ReadString(4, Encoding.ASCII);
                    uint SectionSize = reader.ReadUInt32();

                    Console.WriteLine($"{Signature} {SectionSize}");

                    SectionCommon section = new SectionCommon();

                    switch (Signature)
                    {
                        case "lyt1":
                            LayoutInfo = new LYT1(reader);
                            break;
                        case "txl1":
                            TextureList = new TXL1(reader);
                            break;
                        case "fnl1":
                            FontList = new FNL1(reader);
                            break;
                        case "mat1":
                            MaterialList = new MAT1(reader, this);
                            break;
                        case "pan1":
                            var panel = new PAN1(reader);
                            if (!setRoot)
                            {
                                RootPane = panel;
                                setRoot = true;
                            }

                            SetPane(panel, parentPane);
                            currentPane = panel;
                            break;
                        case "pic1":
                            var picturePanel = new PIC1(reader);

                            SetPane(picturePanel, parentPane);
                            currentPane = picturePanel;
                            break;
                        case "txt1":
                            var textPanel = new TXT1(reader);

                            SetPane(textPanel, parentPane);
                            currentPane = textPanel;
                            break;
                        case "bnd1":
                            var boundsPanel = new BND1(reader);

                            SetPane(boundsPanel, parentPane);
                            currentPane = boundsPanel;
                            break;
                        case "prt1":
                            var partsPanel = new PRT1(reader);

                            SetPane(partsPanel, parentPane);
                            currentPane = partsPanel;
                            break;
                        case "wnd1":
                            var windowPanel = new PRT1(reader);

                            SetPane(windowPanel, parentPane);
                            currentPane = windowPanel;
                            break;
                        case "cnt1":
                            break;
                        case "pas1":
                            if (currentPane != null)
                                parentPane = currentPane;
                            break;
                        case "pae1":
                            currentPane = parentPane;
                            parentPane = currentPane.Parent;
                            break;
                        case "grp1":
                            var groupPanel = new GRP1(reader, this);

                            if (!setGroupRoot)
                            {
                                RootGroup = groupPanel;
                                setGroupRoot = true;
                            }

                            break;
                        case "grs1":
                            break;
                        case "gre1":
                            break;
                        case "usd1":
                            break;
                        //If the section is not supported store the raw bytes
                        default:
                            section.Data = reader.ReadBytes((int)SectionSize);
                            break;
                    }

                    section.Signature = Signature;
                    section.SectionSize = SectionSize;

                    reader.SeekBegin(pos + SectionSize);
                }
            }

            private void SetPane(BasePane pane, BasePane parentPane)
            {
                if (parentPane != null)
                {
                    parentPane.Childern.Add(pane);
                    pane.Parent = parentPane;
                }
            }

            public void Write(FileWriter writer)
            {
                writer.WriteSignature(Magic);
                writer.Write(ByteOrderMark);
                writer.Write(HeaderSize);
                writer.Write(Version);
                writer.Write(uint.MaxValue); //Reserve space for file size later
                writer.Write(ushort.MaxValue); //Reserve space for section count later
                writer.Seek(2); //padding

                //Write the total file size
                using (writer.TemporarySeek(0x0C, System.IO.SeekOrigin.Begin))
                {
                    writer.Write((uint)writer.BaseStream.Length);
                }
            }
        }

        public class BasePane : SectionCommon
        {
            public BasePane Parent { get; set; }

            public List<BasePane> Childern { get; set; } = new List<BasePane>();
        }

        public class TexCoord
        {
            public Vector2F TopLeft { get; set; }
            public Vector2F TopRight { get; set; }
            public Vector2F BottomLeft { get; set; }
            public Vector2F BottomRight { get; set; }

            public TexCoord()
            {
                TopLeft = new Vector2F(0, 0);
				TopRight = new Vector2F(1, 0);
				BottomLeft = new Vector2F(0, 1);
				BottomRight = new Vector2F(1, 1);
            }
        }

        public class TXT1 : PAN1
        {
            public TXT1() : base()
            {
         
            }

            public OriginX HorizontalAlignment
            {
                get => (OriginX)((TextAlignment >> 2) & 0x3);
                set
                {
                    TextAlignment &= unchecked((byte)(~0xC));
                    TextAlignment |= (byte)((byte)(value) << 2);
                }
            }

            public OriginX VerticalAlignment
            {
                get => (OriginX)((TextAlignment) & 0x3);
                set
                {
                    TextAlignment &= unchecked((byte)(~0x3));
                    TextAlignment |= (byte)(value);
                }
            }

            public ushort TextLength;
            public ushort MaxTextLength;
            public ushort MaterialIndex;
            public ushort FontIndex;

            public byte TextAlignment { get; set; }
            public LineAlign LineAlignment { get; set; }
        
            public float ItalicTilt { get; set; }

            public STColor8 FontForeColor { get; set; }
            public STColor8 FontBackColor { get; set; }
            public Vector2F FontSize { get; set; }

            public float CharacterSpace { get; set; }
            public float LineSpace { get; set; }

            public Vector2F ShadowXY { get; set; }
            public Vector2F ShadowXYSize { get; set; }

            public STColor8 ShadowForeColor { get; set; }
            public STColor8 ShadowBackColor { get; set; }

            public float ShadowItalic { get; set; }

            public bool PerCharTransform
            {
                get { return (_flags & 0x10) != 0; }
                set { _flags = value ? (byte)(_flags | 0x10) : unchecked((byte)(_flags & (~0x10))); }
            }
            public bool RestrictedTextLengthEnabled
            {
                get { return (_flags & 0x2) != 0; }
                set { _flags = value ? (byte)(_flags | 0x2) : unchecked((byte)(_flags & (~0x2))); }
            }
            public bool ShadowEnabled
            {
                get { return (_flags & 1) != 0; }
                set { _flags = value ? (byte)(_flags | 1) : unchecked((byte)(_flags & (~1))); }
            }

            private byte _flags;

            public TXT1(FileReader reader) : base(reader)
            {
                TextLength = reader.ReadUInt16();
                MaxTextLength = reader.ReadUInt16();
                MaterialIndex = reader.ReadUInt16();
                FontIndex = reader.ReadUInt16();
                TextAlignment = reader.ReadByte();
                LineAlignment = (LineAlign)reader.ReadByte();
                _flags = reader.ReadByte();
                reader.Seek(1); //padding
                ItalicTilt = reader.ReadSingle();
                uint textOffset = reader.ReadUInt32();
                FontForeColor = STColor8.FromBytes(reader.ReadBytes(4));
                FontBackColor = STColor8.FromBytes(reader.ReadBytes(4));
                FontSize = reader.ReadVec2SY();
                CharacterSpace = reader.ReadSingle();
                LineSpace = reader.ReadSingle();
                ShadowXY = reader.ReadVec2SY();
                ShadowXYSize = reader.ReadVec2SY();
                ShadowForeColor = STColor8.FromBytes(reader.ReadBytes(4));
                ShadowBackColor = STColor8.FromBytes(reader.ReadBytes(4));
                ShadowItalic = reader.ReadSingle();
            }

            public override void Write(FileWriter writer, Header header)
            {
                base.Write(writer, header);
                writer.Write(TextLength);
                writer.Write(MaxTextLength);
                writer.Write(MaterialIndex);
                writer.Write(FontIndex);
                writer.Write(TextAlignment);
                writer.Write(LineAlignment, false);
                writer.Write(_flags);
                writer.Seek(1);
                writer.Write(ItalicTilt);
                writer.Write(0); //text offset
                writer.Write(FontForeColor.ToBytes());
                writer.Write(FontBackColor.ToBytes());
                writer.Write(FontSize);
                writer.Write(CharacterSpace);
                writer.Write(LineSpace);
                writer.Write(ShadowXY);
                writer.Write(ShadowXYSize);
                writer.Write(ShadowForeColor.ToBytes());
                writer.Write(ShadowBackColor.ToBytes());
                writer.Write(ShadowItalic);
            }

            public enum BorderType : byte
            {
                Standard = 0,
                DeleteBorder = 1,
                RenderTwoCycles = 2,
            };

            public enum LineAlign : byte
            {
                Unspecified = 0,
                Left = 1,
                Center = 2,
                Right = 3,
            };
        }

        public class WND1 : PAN1
        {
            public WND1() : base()
            {

            }

            public WND1(FileReader reader) : base(reader)
            {

            }

            public override void Write(FileWriter writer, Header header)
            {
                base.Write(writer, header);
            }
        }

        public class BND1 : PAN1
        {
            public BND1() : base()
            {

            }

            public BND1(FileReader reader) : base(reader)
            {

            }

            public override void Write(FileWriter writer, Header header)
            {
                base.Write(writer, header);
            }
        }

        public class GRP1 : BasePane
        {
            public string Name { get; set; }

            public List<string> Panes { get; set; } = new List<string>();

            public GRP1() : base()
            {

            }

            public GRP1(FileReader reader, Header header)
            {
                ushort numNodes = 0;
                if (header.Version >= 0x05020000)
                {
                    Name = reader.ReadString(34).Replace("\0", string.Empty);
                    numNodes = reader.ReadUInt16();
                }
                else
                {
                    Name = reader.ReadString(24).Replace("\0", string.Empty);
                    numNodes = reader.ReadUInt16();
                    reader.Seek(2); //padding
                }

                for (int i = 0; i < numNodes; i++)
                    Panes.Add(reader.ReadString(24));
            }

            public override void Write(FileWriter writer, Header header)
            {
                if (header.Version >= 0x05020000)
                {
                    writer.WriteString(Name, 34);
                    writer.Write((ushort)Panes.Count);
                }
                else
                {
                    writer.WriteString(Name, 24);
                    writer.Write((ushort)Panes.Count);
                    writer.Seek(2);
                }

                for (int i = 0; i < Panes.Count; i++)
                    writer.WriteString(Panes[i], 24);
            }
        }

        public class PRT1 : PAN1
        {
            public PRT1() : base()
            {

            }

            public PRT1(FileReader reader) : base(reader)
            {

            }

            public override void Write(FileWriter writer, Header header)
            {
                base.Write(writer, header);
            }
        }

        public class PIC1 : PAN1
        {
            public TexCoord[] TexCoords { get; set; }

            public STColor8 ColorTopLeft { get; set; }
            public STColor8 ColorTopRight { get; set; }
            public STColor8 ColorBottomLeft { get; set; }
            public STColor8 ColorBottomRight { get; set; }

            public ushort MaterialIndex { get; set; }

            public PIC1() : base() {
                ColorTopLeft = STColor8.White;
                ColorTopRight = STColor8.White;
                ColorBottomLeft = STColor8.White;
                ColorBottomRight = STColor8.White;
                MaterialIndex = 0;
                TexCoords = new TexCoord[1];
                TexCoords[0] = new TexCoord();
            }

            public PIC1(FileReader reader) : base(reader)
            {
                ColorTopLeft = STColor8.FromBytes(reader.ReadBytes(4));
                ColorTopRight = STColor8.FromBytes(reader.ReadBytes(4));
                ColorBottomLeft = STColor8.FromBytes(reader.ReadBytes(4));
                ColorBottomRight = STColor8.FromBytes(reader.ReadBytes(4));
                MaterialIndex = reader.ReadUInt16();
                byte numUVs = reader.ReadByte();
                reader.Seek(1); //padding
            }

            public override void Write(FileWriter writer, Header header)
            {
                base.Write(writer, header);
                writer.Write(ColorTopLeft.ToBytes());
                writer.Write(ColorTopRight.ToBytes());
                writer.Write(ColorBottomLeft.ToBytes());
                writer.Write(ColorBottomRight.ToBytes());
                writer.Write(MaterialIndex);
                writer.Write(TexCoords != null ? TexCoords.Length : 0);
            }
        }

        public class PAN1 : BasePane
        {
            private byte _flags1;
            private byte _flags2;

            public bool Visible
            {
                get { return (_flags1 & 0x1) == 0x1; }
                set {
                    if (value)
                        _flags1 |= 0x1;
                    else
                        _flags1 &= 0xFE; 
                }
            }

            public bool InfluenceAlpha
            {
                get { return (_flags1 & 0x2) == 0x2; }
                set
                {
                    if (value)
                        _flags1 |= 0x2;
                    else
                        _flags1 &= 0xFD;
                }
            }

            public OriginX originX
            {
                get => (OriginX)((_flags2 & 0xC0) >> 6);
                set
                {
                    _flags2 &= unchecked((byte)(~0xC0));
                    _flags2 |= (byte)((byte)value << 6);
                }
            }

            public OriginY originY
            {
                get => (OriginY)((_flags2 & 0x30) >> 4);
                set
                {
                    _flags2 &= unchecked((byte)(~0x30));
                    _flags2 |= (byte)((byte)value << 4);
                }
            }

            public OriginX ParentOriginX
            {
                get => (OriginX)((_flags2 & 0xC) >> 2);
                set
                {
                    _flags2 &= unchecked((byte)(~0xC));
                    _flags2 |= (byte)((byte)value << 2);
                }
            }

            public OriginY ParentOriginY
            {
                get => (OriginY)((_flags2 & 0x3));
                set
                {
                    _flags2 &= unchecked((byte)(~0x3));
                    _flags2 |= (byte)value;
                }
            }

            public byte Alpha { get; set; }
            public byte Unknown { get; set; }

            public string Name { get; set; }
            public string UserDataInfo { get; set; }

            public Vector3F Translate;
            public Vector3F Rotate;
            public Vector2F Scale;
            public float Width;
            public float Height;

            public PAN1() : base()
            {

            }

            public PAN1(FileReader reader) : base()
            {
                _flags1 = reader.ReadByte();
                _flags2 = reader.ReadByte();
                Alpha = reader.ReadByte();
                Unknown = reader.ReadByte();
                Name = reader.ReadString(0x18).Replace("\0", string.Empty);
                UserDataInfo = reader.ReadString(0x18).Replace("\0", string.Empty);
                Translate = reader.ReadVec3SY();
                Rotate = reader.ReadVec3SY();
                Scale = reader.ReadVec2SY();
                Width = reader.ReadSingle();
                Height = reader.ReadSingle();
            }

            public override void Write(FileWriter writer, Header header)
            {
                writer.Write(_flags1);
                writer.Write(_flags2);
                writer.Write(Alpha);
                writer.Write(Unknown);
                writer.WriteString(Name, 0x18);
                writer.WriteString(UserDataInfo, 0x18);
                writer.Write(Translate);
                writer.Write(Rotate);
                writer.Write(Scale);
                writer.Write(Width);
                writer.Write(Height);
            }

            public enum OriginX : byte
            {
                Center = 0,
                Left = 1,
                Right = 2
            };

            public enum OriginY : byte
            {
                Center = 0,
                Top = 1,
                Bottom = 2
            };
        }

        public class MAT1 : SectionCommon
        {
            public List<Material> Materials { get; set; }

            public MAT1() {
                Materials = new List<Material>();
            }

            public MAT1(FileReader reader, Header header) : base()
            {
                Materials = new List<Material>();

                long pos = reader.Position;

                ushort numMats = reader.ReadUInt16();
                reader.Seek(2); //padding

                uint[] offsets = reader.ReadUInt32s(numMats);
                for (int i = 0; i < numMats; i++)
                {
                    reader.SeekBegin(pos + offsets[i] - 8);
                    Materials.Add(new Material(reader, header));
                }
            }

            public override void Write(FileWriter writer, Header header)
            {
                writer.Write((ushort)Materials.Count);
                writer.Seek(2);
            }
        }

        public class Material
        {
            public string Name { get; set; }

            public STColor8 ForeColor { get; set; }
            public STColor8 BackColor { get; set; }

            public List<TextureRef> TextureMaps { get; set; }
            public List<TextureTransform> TextureTransforms { get; set; }

            private int flags;
            private int unknown;

            public Material()
            {
                TextureMaps = new List<TextureRef>();
                TextureTransforms = new List<TextureTransform>();
            }

            public Material(FileReader reader, Header header) : base()
            {
                TextureMaps = new List<TextureRef>();
                TextureTransforms = new List<TextureTransform>();

                Name = reader.ReadString(0x1C).Replace("\0", string.Empty);
                if (header.Version == 0x8030000)
                {
                    flags = reader.ReadInt32();
                    unknown = reader.ReadInt32();
                    ForeColor = STColor8.FromBytes(reader.ReadBytes(4));
                    BackColor = STColor8.FromBytes(reader.ReadBytes(4));
                }
                else
                {
                    ForeColor = STColor8.FromBytes(reader.ReadBytes(4));
                    BackColor = STColor8.FromBytes(reader.ReadBytes(4));
                    flags = reader.ReadInt32();
                }

                int texCount = flags & 3;
                int mtxCount = (flags & 0xC) >> 2;
                for (int i = 0; i < texCount; i++)
                    TextureMaps.Add(new TextureRef(reader));

                for (int i = 0; i < mtxCount; i++)
                    TextureTransforms.Add(new TextureTransform(reader));
            }

            public void Write(FileWriter writer, Header header)
            {
                writer.WriteString(Name, 0x1C);
                if (header.Version == 0x8030000)
                {
                    writer.Write(flags);
                    writer.Write(unknown);
                    writer.Write(ForeColor);
                    writer.Write(BackColor);
                }
                else
                {
                    writer.Write(ForeColor);
                    writer.Write(BackColor);
                    writer.Write(flags);
                }

                for (int i = 0; i < TextureMaps.Count; i++)
                    TextureMaps[i].Write(writer);

                for (int i = 0; i < TextureTransforms.Count; i++)
                    TextureTransforms[i].Write(writer);
            }
        }

        public class TextureTransform
        {
            public Vector2F Translate;
            public float Rotate;
            public Vector2F Scale;

            public TextureTransform() { }

            public TextureTransform(FileReader reader)
            {
                Translate = reader.ReadVec2SY();
                Rotate = reader.ReadSingle();
                Scale = reader.ReadVec2SY();
            }

            public void Write(FileWriter writer)
            {
                writer.Write(Translate);
                writer.Write(Rotate);
                writer.Write(Scale);
            }
        }

        public class TextureRef
        {
            public ushort ID;
            public byte WrapS;
            public byte WrapT;

            public TextureRef() {}

            public TextureRef(FileReader reader) {
                ID = reader.ReadUInt16();
                WrapS = reader.ReadByte();
                WrapT = reader.ReadByte();
            }

            public void Write(FileWriter writer)
            {
                writer.Write(ID);
                writer.Write(WrapS);
                writer.Write(WrapT);
            }
        }

        public class FNL1 : SectionCommon
        {
            public List<string> Fonts { get; set; }

            public FNL1()
            {
                Fonts = new List<string>();
            }

            public FNL1(FileReader reader) : base()
            {
                Fonts = new List<string>();

                ushort numFonts = reader.ReadUInt16();
                reader.Seek(2); //padding

                long pos = reader.Position;

                uint[] offsets = reader.ReadUInt32s(numFonts);
                for (int i = 0; i < offsets.Length; i++)
                {
                    reader.SeekBegin(offsets[i] + pos);
                }
            }

            public override void Write(FileWriter writer, Header header)
            {
                writer.Write((ushort)Fonts.Count);
                writer.Seek(2);

                //Fill empty spaces for offsets later
                long pos = writer.Position;
                writer.Write(new uint[Fonts.Count]);

                //Save offsets and strings
                for (int i = 0; i < Fonts.Count; i++)
                {
                    writer.WriteUint32Offset(pos + (i * 4), pos);
                    writer.WriteString(Fonts[i]);
                }
            }
        }

        public class TXL1 : SectionCommon
        {
            public List<string> Textures { get; set; }

            public TXL1()
            {
                Textures = new List<string>();
            }

            public TXL1(FileReader reader) : base()
            {
                Textures = new List<string>();

                ushort numTextures = reader.ReadUInt16();
                reader.Seek(2); //padding

                long pos = reader.Position;

                uint[] offsets = reader.ReadUInt32s(numTextures);
                for (int i = 0; i < offsets.Length; i++)
                {
                    reader.SeekBegin(offsets[i] + pos);
                }
            }

            public override void Write(FileWriter writer, Header header)
            {
                writer.Write((ushort)Textures.Count);
                writer.Seek(2);

                //Fill empty spaces for offsets later
                long pos = writer.Position;
                writer.Write(new uint[Textures.Count]);

                //Save offsets and strings
                for (int i = 0; i < Textures.Count; i++)
                {
                    writer.WriteUint32Offset(pos + (i * 4), pos);
                    writer.WriteString(Textures[i]);
                }
            }
        }

        public class LYT1 : SectionCommon
        {
            public bool DrawFromCenter { get; set; }

            public float Width { get; set; }
            public float Height { get; set; }

            public float MaxPartsWidth { get; set; }
            public float MaxPartsHeight { get; set; }
            public string Name { get; set; }

            public LYT1()
            {
                DrawFromCenter = false;
                Width = 0;
                Height = 0;
                MaxPartsWidth = 0;
                MaxPartsHeight = 0;
                Name = "";
            }

            public LYT1(FileReader reader)
            {
                DrawFromCenter = reader.ReadBoolean();
                reader.Seek(3); //padding
                Width = reader.ReadSingle();
                Height = reader.ReadSingle();
                MaxPartsWidth = reader.ReadSingle();
                MaxPartsHeight = reader.ReadSingle();
                Name = reader.ReadZeroTerminatedString();
            }

            public override void Write(FileWriter writer, Header header)
            {
                writer.Write(DrawFromCenter);
                writer.Seek(3);
                writer.Write(Width);
                writer.Write(Height);
                writer.Write(MaxPartsWidth);
                writer.Write(MaxPartsHeight);
                writer.Write(Name);
            }
        }

        public class SectionCommon
        {
            internal string Signature { get; set; }
            internal uint SectionSize { get; set; }

            internal byte[] Data { get; set; }

            public virtual void Write(FileWriter writer, Header header)
            {
                writer.WriteSignature(Signature);
                if (Data != null)
                {
                    writer.Write(Data.Length);
                    writer.Write(Data);
                }
            }
        }
    }
}
