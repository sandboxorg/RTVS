﻿using System.Diagnostics.CodeAnalysis;
using Microsoft.Languages.Core.Text;
using Microsoft.R.Core.AST.Definitions;
using Microsoft.R.Core.Tokens;
using Microsoft.R.Editor.Validation.Definitions;
using Microsoft.R.Core.Parser;
using System;

namespace Microsoft.R.Editor.Validation.Errors
{
    [ExcludeFromCodeCoverage]
    public class ValidationErrorBase : TextRange, IValidationError
    {
        /// <summary>
        /// AST node in the parse tree
        /// </summary>
        public IAstNode Node { get; private set; }

        /// <summary>
        /// Token that produced the error.
        /// </summary>
        public RToken Token { get; private set; }

        /// <summary>
        /// Error or warning message.
        /// </summary>
        public string Message { get; private set; }

        /// <summary>
        /// Error location
        /// </summary>
        public ErrorLocation Location { get; private set; }

        /// <summary>
        /// Error severity
        /// </summary>
        public ErrorSeverity Severity { get; private set; }

        public ValidationErrorBase(IAstNode node, RToken token, string message, ErrorLocation location, ErrorSeverity severity) :
            base(GetLocationRange(node, token, location))
        {
            Node = node;
            Token = token;

            Message = message;
            Severity = severity;
            Location = location;
        }

        private static ITextRange GetLocationRange(IAstNode node, RToken token, ErrorLocation location)
        {
            ITextRange itemRange = node != null ? node : token as ITextRange;

            //switch (location)
            //{
            //    case ErrorLocation.BeforeToken:
            //        return new TextRange(Math.Max(0, itemRange.Start-1), 1);

            //    case ErrorLocation.AfterToken:
            //        return new TextRange(itemRange.End, 1);
            //}

            return itemRange;
        }
    }
}