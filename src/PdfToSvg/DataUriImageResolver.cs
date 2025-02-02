﻿// Copyright (c) PdfToSvg.NET contributors.
// https://github.com/dmester/pdftosvg.net
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PdfToSvg
{
    /// <summary>
    /// Creates data URIs for images.
    /// </summary>
    public class DataUriImageResolver : ImageResolver
    {
        /// <inheritdoc/>
        public override string ResolveImageUrl(Image image, CancellationToken cancellationToken)
        {
            return image.ToDataUri(cancellationToken);
        }
    }
}
