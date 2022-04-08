// ------------------------------------------------------------------------------
// Copyright (c) 2016 - Allen Technologies www.allentek.com. All Rights Reserved.
// Author: Tai H. Le <tle@allentek.com>
// 
// Main Window - Text Search
// ------------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace e3tools
{
    public partial class MainWindow
    {
        string _searchText = string.Empty; // to remember last search text
        int _searchCount = 0;
        RichTextBox _tb = null;

        private void BtnSearch_Click(object sender, RoutedEventArgs e)
        {
            _searchText = TxtSearch.Text;
            _tb = ((TcLogTabs.SelectedItem as TabItem).Content as RichTextBox);

            // TODO: make it async -- may take too long
            if (!_hilightWords.Contains(_searchText))
            {
                if (_hilightWords.Count > 0)
                {
                    _hilightWords.Clear();
                    DoHighLightSelectionText();
                }
                _hilightWords.Add(TxtSearch.Text);
                _searchCount = DoHighLightSelectionText();
            }
            else if (_searchCount <= 0)
            {
                MessageBox.Show("Text not found: '" + _searchText + "'");
                return;
            }

            TextPointer currentPosition = _tb.CaretPosition;
            if (null == currentPosition) currentPosition = _tb.Document.ContentStart;

            TextRange foundText = GetTextRangeFromPosition(ref currentPosition, _tb.Document,
                TxtSearch.Text, FindOptions.None, LogicalDirection.Forward);

            if (!SetFoundTextLocation(foundText, LogicalDirection.Forward))
            {
                if (_searchCount > 0)
                {
                    MessageBox.Show("Searched to the end. Found " + _searchCount);
                }
                else
                {
                    MessageBox.Show("Text not found: '" + _searchText + "'");
                }
            }
        }

        private bool SetFoundTextLocation(TextRange foundText, LogicalDirection findDirection)
        {
            if (null != foundText)
            {
                _tb.CaretPosition = foundText.Start;
                _tb.Selection.Select(foundText.Start, foundText.End);

                Rect screenPos = _tb.Selection.Start.GetCharacterRect(findDirection);
                double offset = screenPos.Top + _tb.VerticalOffset;
                _tb.ScrollToVerticalOffset(offset - _tb.ActualHeight / 2);
                _tb.Focus();
                return true;
            }
            else
            {
                return false;
            }
        }

        int DoHighLightSelectionText()
        {
            int ret = 0;
            try
            {
                if (_tb.Document == null) return ret;
                TextRange documentRange = new TextRange(_tb.Document.ContentStart, _tb.Document.ContentEnd);
                documentRange.ClearAllProperties();

                if (_hilightWords.Count <= 0)
                {
                    documentRange.ClearAllProperties();
                    _hiLightTags.Clear();
                    return ret;
                }


                TextPointer navigator = _tb.Document.ContentStart;
                while (navigator.CompareTo(_tb.Document.ContentEnd) < 0)
                {
                    TextPointerContext context = navigator.GetPointerContext(LogicalDirection.Backward);
                    if (context == TextPointerContext.ElementStart && navigator.Parent is Run)
                    {
                        ret += CheckWordsInRun((Run)navigator.Parent);
                    }
                    navigator = navigator.GetNextContextPosition(LogicalDirection.Forward);
                }
                Format();
            }
            catch
            {
            }

            return ret;
        }

        private void _tb_TextChanged(object sender, TextChangedEventArgs e)
        {
            DoHighLightSelectionText();
        }

        void Format()
        {
            _tb.TextChanged -= this._tb_TextChanged;

            for (int i = 0; i < _hiLightTags.Count; i++)
            {
                TextRange range = new TextRange(_hiLightTags[i].StartPosition, _hiLightTags[i].EndPosition);
                range.ApplyPropertyValue(TextElement.ForegroundProperty, new SolidColorBrush(Colors.Blue));
                range.ApplyPropertyValue(TextElement.BackgroundProperty, new SolidColorBrush(Colors.Yellow));
                range.ApplyPropertyValue(TextElement.FontWeightProperty, FontWeights.Bold);
            }
            _hiLightTags.Clear();
            _tb.TextChanged += this._tb_TextChanged;
        }

        int CheckWordsInRun(Run run)
        {
            int ret = 0;
            string txt = run.Text;
            foreach (string word in _hilightWords)
            {
                if (txt.Contains(word))
                {
                    Tag t = new Tag();
                    int pos = txt.IndexOf(word);
                    t.StartPosition = run.ContentStart.GetPositionAtOffset(pos, LogicalDirection.Forward);
                    t.EndPosition = run.ContentStart.GetPositionAtOffset(pos + word.Length, LogicalDirection.Backward);
                    t.Word = word;
                    _hiLightTags.Add(t);
                    ret++;
                }
            }

            return ret;
        }        

        new struct Tag
        {
            public TextPointer StartPosition;
            public TextPointer EndPosition;
            public string Word;
        }
        List<Tag> _hiLightTags = new List<Tag>();
        List<string> _hilightWords = new List<string>();

        /// <summary>
        /// This class represents the possible options for search operation.
        /// </summary>
        [Flags]
        public enum FindOptions
        {
            /// <summary>
            /// Perform case-insensitive non-word search.
            /// </summary>
            None = 0x00000000,
            /// <summary>
            /// Perform case-sensitive search.
            /// </summary>
            MatchCase = 0x00000001,
            /// <summary>
            /// Perform the search against whole word.
            /// </summary>
            MatchWholeWord = 0x00000002,
        }

        /// <summary>
        /// Finds the corresponding<see cref="TextRange"/> instance 
        /// representing the input string given a specified text pointer position.
        /// </summary>
        /// <param name="position">the current text position</param>
        /// <param name="textToFind">input text</param>
        /// <param name="findOptions">the search option</param>
        /// <returns>
        /// An<see cref="TextRange"/> instance represeneting the matching
        /// string withing the text container.
        /// </returns>
        public TextRange GetTextRangeFromPosition(ref TextPointer position,
            FlowDocument inputDocument, String input, FindOptions findOptions,
            LogicalDirection findDirection)
        {
            Boolean matchCase = (findOptions & FindOptions.MatchCase) == FindOptions.MatchCase;
            Boolean matchWholeWord = (findOptions & FindOptions.MatchWholeWord)
                                                        == FindOptions.MatchWholeWord;

            TextRange textRange = null;

            while (position != null)
            {
                if (position.CompareTo(inputDocument.ContentEnd) == 0)
                {
                    break;
                }

                if (position.GetPointerContext(findDirection) == TextPointerContext.Text)
                {
                    String textRun = position.GetTextInRun(findDirection);
                    StringComparison stringComparison = matchCase ?
                        StringComparison.CurrentCulture : StringComparison.CurrentCultureIgnoreCase;
                    Int32 indexInRun = textRun.IndexOf(input, stringComparison);

                    if (indexInRun >= 0)
                    {
                        position = position.GetPositionAtOffset(indexInRun);
                        TextPointer nextPointer = position.GetPositionAtOffset(input.Length);
                        textRange = new TextRange(position, nextPointer);

                        if (matchWholeWord)
                        {
                            if (IsWholeWord(inputDocument, textRange)) // Test if the "textRange" represents a word.
                            {
                                // If a WholeWord match is found, directly terminate the loop.
                                position = position.GetPositionAtOffset(input.Length, findDirection);
                                break;
                            }
                            else
                            {
                                // If a WholeWord match is not found, go to next recursion to find it.
                                position = position.GetPositionAtOffset(input.Length, findDirection);
                                return GetTextRangeFromPosition(ref position, inputDocument, input, findOptions, findDirection);
                            }
                        }
                        else
                        {
                            // If a none-WholeWord match is found, directly terminate the loop.
                            position = position.GetPositionAtOffset(input.Length, findDirection);
                            break;
                        }
                    }
                    else
                    {
                        // If a match is not found, go over to the next context position after the "textRun".
                        position = position.GetPositionAtOffset(textRun.Length, findDirection);
                    }
                }
                else
                {
                    //If the current position doesn't represent a text context position, go to the next context position.
                    // This can effectively ignore the formatting or embed element symbols.
                    position = position.GetNextContextPosition(findDirection);
                }
            }

            return textRange;
        }

        /// <summary>
        /// determines if the specified character is a valid word character.
        /// here only underscores, letters, and digits are considered to be valid.
        /// </summary>
        /// <param name="character">character specified</param>
        /// <returns>Boolean value didicates if the specified character is a valid word character</returns>
        private Boolean IsWordChar(Char character)
        {
            return Char.IsLetterOrDigit(character) || character == '_';
        }

        /// <summary>
        /// Tests if the string within the specified<see cref="TextRange"/>instance is a word.
        /// </summary>
        /// <param name="textRange"><see cref="TextRange"/>instance to test</param>
        /// <returns>test result</returns>
        private Boolean IsWholeWord(FlowDocument inputDocument, TextRange textRange)
        {
            Char[] chars = new Char[1];

            if (textRange.Start.CompareTo(inputDocument.ContentStart) == 0 || textRange.Start.IsAtLineStartPosition)
            {
                textRange.End.GetTextInRun(LogicalDirection.Forward, chars, 0, 1);
                return !IsWordChar(chars[0]);
            }
            else if (textRange.End.CompareTo(inputDocument.ContentEnd) == 0)
            {
                textRange.Start.GetTextInRun(LogicalDirection.Backward, chars, 0, 1);
                return !IsWordChar(chars[0]);
            }
            else
            {
                textRange.End.GetTextInRun(LogicalDirection.Forward, chars, 0, 1);
                if (!IsWordChar(chars[0]))
                {
                    textRange.Start.GetTextInRun(LogicalDirection.Backward, chars, 0, 1);
                    return !IsWordChar(chars[0]);
                }
            }

            return false;
        }
    }
}
