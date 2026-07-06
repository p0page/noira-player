using System;
using System.Collections.Generic;
using NextGenEmby.Core.Emby;

namespace NextGenEmby.Core.Diagnostics
{
    public sealed class DevelopmentHomeMediaRow
    {
        public DevelopmentHomeMediaRow(
            string title,
            string collectionType,
            string parentId,
            string sectionId,
            EmbyHomeSection section,
            IReadOnlyList<EmbyMediaItem> items)
        {
            Title = string.IsNullOrWhiteSpace(title) ? "Home section" : title;
            CollectionType = collectionType ?? "";
            ParentId = parentId ?? "";
            SectionId = sectionId ?? "";
            Section = section ?? new EmbyHomeSection();
            ParentItem = Section.ParentItem ?? new EmbyMediaItem();
            Items = items ?? Array.Empty<EmbyMediaItem>();
        }

        public string Title { get; }

        public string CollectionType { get; }

        public string ParentId { get; }

        public string SectionId { get; }

        public EmbyHomeSection Section { get; }

        public EmbyMediaItem ParentItem { get; }

        public IReadOnlyList<EmbyMediaItem> Items { get; }
    }
}
