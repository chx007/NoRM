using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Norm.BSON;

namespace Norm.Commands.Qualifiers
{
    //HACK: 增加实体关联的数据库属性查询
    public class RefDbQualifier : QualifierCommand
    {
        internal RefDbQualifier(string value)
            : base("$db", value)
        {
        }
    }
}
