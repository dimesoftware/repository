using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;

namespace Dime.Repositories
{
    [KnownType(typeof(Page<>))]
    public class Page<T> : IPage<T>
    {
        public Page()
        {
        }

        public Page(IEnumerable<T> data)
        {
            Data = data;
            Summary = [];
        }

        public Page(IEnumerable<T> data, int total)
            : this(data)
        {
            Total = total;
        }

        public Page(IEnumerable<T> data, int total, string message)
            : this(data, total)
        {
            Message = message;
        }

        public Page(IEnumerable<T> data, int total, string message, IEnumerable<dynamic> summary)
            : this(data, total, message)
        {
            Summary = summary != null ? [.. summary] : [];
        }

        public IEnumerable<T> Data { get; set; }

        public int Total { get; set; }

        public string Message { get; set; }

        public List<dynamic> Summary { get; }
    }
}