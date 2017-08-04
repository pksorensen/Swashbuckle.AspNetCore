﻿using System;
using System.Collections.Generic;
using Xunit;

namespace Swashbuckle.AspNetCore.SwaggerGen.Test
{
    public class TypeExtensionsTests
    {
        [Theory]
        [InlineData(typeof(ComplexType), "ComplexType")]
        [InlineData(typeof(IEnumerable<string>), "IEnumerable[String]")]
        [InlineData(typeof(IDictionary<string, decimal>), "IDictionary[String,Decimal]")]
        public void FriendlyId_ReturnsNonQualifiedFriendlyId_IfFullyQualifiedFlagIsUnset(
            Type systemType,
            string expectedReturnValue)
        {
            Assert.Equal(expectedReturnValue, systemType.FriendlyId());
        }

        [Theory]
        [InlineData(typeof(ComplexType), "Swashbuckle.AspNetCore.SwaggerGen.Test.ComplexType")]
        [InlineData(typeof(IEnumerable<string>), "System.Collections.Generic.IEnumerable[System.String]")]
        [InlineData(typeof(IDictionary<string, decimal>), "System.Collections.Generic.IDictionary[System.String,System.Decimal]")]
        [InlineData(typeof(ContainingType.NestedType), "Swashbuckle.AspNetCore.SwaggerGen.Test.ContainingType.NestedType")]
        public void FriendlyId_ReturnsFullQualifiedFriendlyId_IfFullyQualifiedFlagIsSet(
            Type systemType,
            string expectedReturnValue)
        {
            Assert.Equal(expectedReturnValue, systemType.FriendlyId(true));
        }

        //[Fact(Skip = "Need to figure out dependencies for using [EnumMemberAttribute] in Core")]
        //public void GetEnumNamesForSerialization_HonorsEnumMemberAttributes()
        //{
        //    throw new NotImplementedException();
        //}
    }
}