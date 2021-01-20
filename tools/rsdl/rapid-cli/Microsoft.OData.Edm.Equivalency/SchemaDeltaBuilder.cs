using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.OData.Edm;
using Microsoft.OData.Edm.Vocabularies;

namespace Microsoft.OData.Edm
{
    internal partial class SchemaDeltaBuilder
    {
        private readonly HashSet<object> visited = new HashSet<object>();
        private readonly List<(PropertyPath, String, EdmLocation, EdmLocation)> errors = new List<(PropertyPath, String, EdmLocation, EdmLocation)>();

        public IEnumerable<(PropertyPath Path, String Message, EdmLocation Left, EdmLocation Right)> Errors => errors;

        protected void Visit(String a, String b, PropertyPath path)
        {
            if (!String.Equals(a, b))
                Report(path, $"different values '{a}' != '{b}'");
        }
        protected void Visit(Boolean a, Boolean b, PropertyPath path) { if (!Boolean.Equals(a, b)) Report(path, $"different values '{a}' != '{b}'"); }
        protected void Visit(Date a, Date b, PropertyPath path) { if (!Date.Equals(a, b)) Report(path, $"different values '{a}' != '{b}'"); }
        protected void Visit(DateTimeOffset a, DateTimeOffset b, PropertyPath path) { if (!DateTimeOffset.Equals(a, b)) Report(path, $"different values '{a}' != '{b}'"); }
        protected void Visit(Decimal a, Decimal b, PropertyPath path) { if (!Decimal.Equals(a, b)) Report(path, $"different values '{a}' != '{b}'"); }
        protected void Visit(TimeSpan a, TimeSpan b, PropertyPath path) { if (!TimeSpan.Equals(a, b)) Report(path, $"different values '{a}' != '{b}'"); }
        protected void Visit(Double a, Double b, PropertyPath path) { if (!Double.Equals(a, b)) Report(path, $"different values '{a}' != '{b}'"); }
        protected void Visit(Guid a, Guid b, PropertyPath path) { if (!Guid.Equals(a, b)) Report(path, $"different values '{a}' != '{b}'"); }
        protected void Visit(Int64 a, Int64 b, PropertyPath path) { if (!Int64.Equals(a, b)) Report(path, $"different values '{a}' != '{b}'"); }
        protected void Visit(TimeOfDay a, TimeOfDay b, PropertyPath path) { if (!TimeOfDay.Equals(a, b)) Report(path, $"different values '{a}' != '{b}'"); }
        protected void Visit(int? a, int? b, PropertyPath path) { if (!Nullable<int>.Equals(a, b)) Report(path, $"different values '{a}' != '{b}'"); }
        protected void Visit(bool? a, bool? b, PropertyPath path) { if (!Nullable<bool>.Equals(a, b)) Report(path, $"different values '{a}' != '{b}'"); }
        protected void Visit(Byte[] a, Byte[] b, PropertyPath path) { if (!Enumerable.SequenceEqual(a, b)) Report(path, $"different values '{a}' != '{b}'"); }
        protected void Visit<T>(T a, T b, PropertyPath path) where T : Enum { { if (!object.Equals(a, b)) Report(path, $"different values '{a}' != '{b}'"); } }

        protected void Visit(EdmReferentialConstraintPropertyPair a, EdmReferentialConstraintPropertyPair b, PropertyPath path)
        {
            Visit(a.DependentProperty, b.DependentProperty, path + "DependentProperty");
            Visit(a.PrincipalProperty, b.PrincipalProperty, path + "PrincipalProperty");
        }

        protected void VisitSeq<T>(IEnumerable<T> a, IEnumerable<T> b, Action<T, T, PropertyPath> visit, PropertyPath path) where T : class
        {
            var aa = a.GetEnumerator();
            var bb = b.GetEnumerator();
            for (var i = 0; ; i++)
            {
                var aHasMoved = aa.MoveNext();
                var bHasMoved = bb.MoveNext();
                if (aHasMoved && bHasMoved)
                {
                    visit(aa.Current, bb.Current, path + i.ToString());
                }
                else if (aHasMoved || bHasMoved)
                {
                    // TODO, report non matching length
                    break;
                }
                else
                {
                    break;
                }
            }
        }

        protected void VisitSet(
            IEnumerable<Vocabularies.IEdmVocabularyAnnotation> a,
            IEnumerable<Vocabularies.IEdmVocabularyAnnotation> b,
            Action<Vocabularies.IEdmVocabularyAnnotation, Vocabularies.IEdmVocabularyAnnotation, PropertyPath> visit,
            PropertyPath path)
        {
            var aa = a.ToDictionary(ai => ai.Term.FullName() + "/" + ai.Target.GetHashCode().ToString());
            var bb = b.ToDictionary(bi => bi.Term.FullName() + "/" + bi.Target.GetHashCode().ToString());

            var keys = aa.Keys.Concat(bb.Keys).Distinct();
            foreach (var key in keys)
            {
                if (!aa.ContainsKey(key))
                {
                    Report(path, $"missing annotation for Term {bb[key].Term.FullName()} on right");
                }
                else if (!bb.ContainsKey(key))
                {
                    Report(path, $"missing annotation for Term {aa[key].Term.FullName()} on left");
                }
                else
                {
                    visit(aa[key], bb[key], path);
                }
            }

            // for (var i = 0; ; i++)
            // {
            //     var aHasMoved = aa.MoveNext();
            //     var bHasMoved = bb.MoveNext();
            //     if (aHasMoved && bHasMoved)
            //     {
            //         visit(aa.Current, bb.Current, path + i.ToString());
            //     }
            //     else if (aHasMoved || bHasMoved)
            //     {
            //         // TODO, report non matching length
            //         break;
            //     }
            //     else
            //     {
            //         break;
            //     }
            // }
        }

        protected void VisitNamedSeq<T>(IEnumerable<T> e1, IEnumerable<T> e2, Action<T, T, PropertyPath> visit, PropertyPath path, string property) where T : class, IEdmNamedElement
        {
            var pairs = e1.FullOuterJoin(e2, i => i.Name, i => i.Name);
            foreach (var (name, a, b) in pairs)
            {
                if (a != null && b != null)
                {
                    visit(a, b, path + (property + ":" + name));
                }
                else if (a == null)
                {
                    Report(path, $"missing item with name {name} on left");
                }
                else if (b == null)
                {
                    var loc = b is IEdmLocatable eloc ? eloc.Location : null;
                    Report(path, $"missing item with name {name} on right");
                }
            }
        }

        private class UnknwonEdmLocation : EdmLocation
        {
            public override string ToString() => string.Empty;
            public static UnknwonEdmLocation Instance = new UnknwonEdmLocation();
        }

        protected void Report(PropertyPath path, string message)
        {
            var (l, r) = locations.Count > 0 ? locations.Peek() : (UnknwonEdmLocation.Instance, UnknwonEdmLocation.Instance);
            errors.Add((path, message, l, r));
        }

        // check for reference equality and null, report on it and
        // return true if no further checks/visits are required.
        protected bool IsReferenceCheckComplete<T>(T a, T b, PropertyPath path)
        {
            if (object.ReferenceEquals(a, b))
            {
                return true;
            }
            if (a == null && b == null)
            {
                return true;
            }
            if (a == null && b != null)
            {
                Report(path, "left == null, right not");
                return true;
            }
            if (a != null && b == null)
            {
                Report(path, "right == null, left not");
                return true;
            }
            return false;
        }


        protected void CheckTypeEquality<T>(T a, T b, IList<Type> types, PropertyPath path)
        {
            static Type FindInterface(T obj, IList<Type> interfaces)
            {
                var t = obj.GetType();
                return interfaces.SingleOrDefault(i => i.IsAssignableFrom(t));
            }

            var ai = FindInterface(a, types);
            var bi = FindInterface(b, types);
            if (ai != null && bi != null && ai != bi)
            {
                Report(path, $"different types '{ai.Name}' != '{bi.Name}'");
            }
        }


        public string GetPath(IEdmVocabularyAnnotatable a)
        {
            if (a is IEdmAction aAction)
            {
                return aAction.FullName();
            }
            else if (a is IEdmActionImport aActionImport)
            {
                return aActionImport.Name;
            }
            else if (a is IEdmComplexType aComplexType)
            {
                return aComplexType.ToString();
            }
            else if (a is IEdmEntityContainer aEntityContainer)
            {
                return aEntityContainer.ToString();
            }
            else if (a is IEdmEntitySet aEntitySet)
            {
                return aEntitySet.ToString();
            }
            else if (a is IEdmEntityType aEntityType)
            {
                return aEntityType.ToString();
            }
            else if (a is IEdmEnumMember aEnumMember)
            {
                return aEnumMember.ToString();
            }
            else if (a is IEdmEnumType aEnumType)
            {
                return aEnumType.ToString();
            }
            else if (a is IEdmFunction aFunction)
            {
                return aFunction.ToString();
            }
            else if (a is IEdmFunctionImport aFunctionImport)
            {
                return aFunctionImport.ToString();
            }
            else if (a is IEdmNavigationProperty aNavigationProperty)
            {
                return aNavigationProperty.ToString();
            }
            else if (a is IEdmOperationReturn aOperationReturn)
            {
                return aOperationReturn.ToString();
            }
            else if (a is IEdmOptionalParameter aOptionalParameter)
            {
                return aOptionalParameter.ToString();
            }
            else if (a is IEdmPathType aPathType)
            {
                return aPathType.ToString();
            }
            else if (a is IEdmPrimitiveType aPrimitiveType)
            {
                return aPrimitiveType.ToString();
            }
            else if (a is IEdmSingleton aSingleton)
            {
                return aSingleton.ToString();
            }
            else if (a is IEdmStructuralProperty aStructuralProperty)
            {
                return aStructuralProperty.ToString();
            }
            else if (a is IEdmTerm aTerm)
            {
                return aTerm.ToString();
            }
            else if (a is IEdmTypeDefinition aTypeDefinition)
            {
                return aTypeDefinition.ToString();
            }
            else if (a is IEdmUntypedType aUntypedType)
            {
                return aUntypedType.ToString();
            }
            else
            {
                throw new NotSupportedException();
            }
        }

        #region locations

        private readonly Stack<(EdmLocation, EdmLocation)> locations = new Stack<(EdmLocation, EdmLocation)>();

        private IDisposable PushLocation(object a, object b)
        {
            var aloc = a as IEdmLocatable;
            var bloc = b as IEdmLocatable;
            if (aloc != null || bloc != null)
            {
                locations.Push((aloc?.Location, bloc?.Location));
                return new AnonymousDisposable(() => locations.Pop());
            }
            else
            {
                return NullDisposable.Instance;
            }
        }

        class NullDisposable : IDisposable
        {
            private NullDisposable() { }

            public void Dispose() { }
            public static IDisposable Instance = new NullDisposable();
        }

        class AnonymousDisposable : IDisposable
        {
            private readonly Action action;

            public AnonymousDisposable(Action action) { this.action = action; }

            public void Dispose() { action(); }
        }
    }
    #endregion
}