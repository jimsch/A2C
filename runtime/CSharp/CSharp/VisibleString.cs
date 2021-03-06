﻿using System;
using System.Collections.Generic;
using System.Text;

namespace A2C
{
    public class VisibleString : GenericString
    {
        static readonly Tag s_Tag = new Tag (TagClass.Universal, 26, TagType.Implicit);


        //
        //  Various initializers
        //

        public VisibleString ()
        {
        }

        public VisibleString (string str)
        {
            m_str = str;
        }

        public VisibleString (VisibleString val)
        {
            m_str = val.m_str;
        }

        static public VisibleString Create () { return new VisibleString (); }

        //
        //  Get/Set functions
        //

        //
        //  Overrider functions
        //

        protected override void _Decode (A2C_FLAGS flags, bool fDecodeAsDer, Context ctxt, Tag[] tagChild, ParserStream stm)
        {
            Tag[] tagsAll = Tag.Append (tagChild, s_Tag);
            _DecodeTags (flags, fDecodeAsDer, ctxt, tagsAll, stm);
        }

        protected override void _Encode (A2C_FLAGS flags, bool fEncodeAsDer, Context cctxt, Tag[] tag, Stream stm)
        {
            Tag[] tagsAll = Tag.Append (tag, s_Tag);
            _EncodeTags (flags, fEncodeAsDer, cctxt, tagsAll, stm);
        }

        public override bool CheckCharacterSet ()
        {
            return true;
        }

    }
}
