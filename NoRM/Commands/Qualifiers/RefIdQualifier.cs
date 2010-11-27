using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Norm.BSON;

namespace Norm.Commands.Qualifiers
{
    //HACK: 增加实体关联的$id属性查询
    public class RefIdQualifier : QualifierCommand
    {
        internal RefIdQualifier(object value)
            : base("$id", value)
        {
        }
    }
}
