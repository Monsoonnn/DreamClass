using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using TMPro;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

namespace HMStudio.EasyQuiz
{
    public class QuestionViewer : MonoBehaviour
    {
        [Header("Data")]
        [Tooltip("Enter the ID of the question to load")]
        public int questionID;

        [TextArea]
        [Tooltip("Question content")]
        public string questionText;

        [Tooltip("List of options (unlimited)")]
        public List<string> options = new List<string>();

        [Tooltip("Correct answer")] public string correctAnswer;

        [Header("UI Text References (TextMeshPro)")]
        [SerializeField]
        private TextMeshProUGUI _tmpQuestion;

        [SerializeField] private List<TextMeshProUGUI> _lstOptions = new List<TextMeshProUGUI>();

        // Update status variables used to display HelpBox on the Inspector
        [HideInInspector] public string updateStatusMessage = "";
        [HideInInspector] public bool updateStatusSuccess = false;

        public string excelFilePath;

        public QuizDatabase quizDatabase;  // Assign trong Inspector
        [Tooltip("Subject Index (0-based)")] public int subjectID = 0;
        [Tooltip("Chapter Index (0-based)")] public int chapterID = 0;

        // Cached API data for current chapter
        private List<APIQuestion> cachedAPIQuestions;

        // Current API Question ID (for submit)
        private string currentAPIQuestionId;
        
        /// <summary>
        /// Lấy API Question ID hiện tại (MongoDB _id)
        /// </summary>
        public string CurrentAPIQuestionId => currentAPIQuestionId;

        private void OnValidate()
        {
            if (quizDatabase != null && quizDatabase.DataMode == QuizDataMode.Excel)
            {
                excelFilePath = quizDatabase.GetExcelPath(subjectID, chapterID);
            }
        }

        public void UpdateExcelPath()
        {
            if (quizDatabase != null)
            {
                if (quizDatabase.DataMode == QuizDataMode.Excel)
                {
                    excelFilePath = quizDatabase.GetExcelPath(subjectID, chapterID);
                    Debug.Log("Updated path from QuizDatabase (Excel mode): " + excelFilePath);
                }
                else
                {
                    // API mode - cache questions từ chapter hiện tại
                    CacheAPIQuestions();
                    Debug.Log($"Updated from QuizDatabase (API mode): Subject={subjectID}, Chapter={chapterID}");
                }
            }
            else
            {
                excelFilePath = PlayerPrefs.GetString("SelectedExcelPath", "");
                Debug.Log("Fallback to PlayerPrefs path: " + excelFilePath);
            }
        }

        /// <summary>
        /// Cache câu hỏi từ API cho chapter hiện tại
        /// </summary>
        private void CacheAPIQuestions()
        {
            cachedAPIQuestions = null;
            var chapter = QuizAPIService.Instance.GetChapter(subjectID, chapterID);
            if (chapter != null)
            {
                cachedAPIQuestions = chapter.Questions;
                Debug.Log($"[QuestionViewer] Cached {cachedAPIQuestions.Count} questions from API");
            }
        }

        /// <summary>
        /// Load câu hỏi - tự động chọn Excel hoặc API dựa trên mode
        /// </summary>
        public void LoadQuestion()
        {
            if (quizDatabase != null && quizDatabase.DataMode == QuizDataMode.API)
            {
                LoadQuestionFromAPI();
            }
            else
            {
                LoadQuestionFromExcel();
            }
        }

        /// <summary>
        /// Load câu hỏi từ API cache
        /// </summary>
        public void LoadQuestionFromAPI()
        {
            if (cachedAPIQuestions == null)
            {
                CacheAPIQuestions();
            }

            if (cachedAPIQuestions == null || cachedAPIQuestions.Count == 0)
            {
                Debug.LogError("[QuestionViewer] No API questions cached!");
                questionText = "";
                options.Clear();
                correctAnswer = "";
                return;
            }

            // Tìm câu hỏi theo LocalId
            var question = cachedAPIQuestions.Find(q => q.LocalId == questionID);
            if (question == null)
            {
                Debug.LogError($"[QuestionViewer] Question with LocalId={questionID} not found!");
                questionText = "";
                options.Clear();
                correctAnswer = "";
                return;
            }

            // Load dữ liệu
            questionText = question.QuestionText;
            options = new List<string>(question.Options);
            correctAnswer = question.CorrectAnswer;
            currentAPIQuestionId = question.Id; // Lưu API ID để submit

            // Nếu correctAnswer là key (A, B, C, D), convert sang text
            if (correctAnswer.Length == 1 && options.Count > 0)
            {
                int answerIndex = correctAnswer[0] - 'A';
                if (answerIndex >= 0 && answerIndex < options.Count)
                {
                    correctAnswer = options[answerIndex];
                }
            }

            // ShuffleOptions(); // Bỏ shuffle để giữ thứ tự câu trả lời
            UpdateTextFields();

            Debug.Log($"[QuestionViewer] Loaded from API: Q{questionID} - {questionText.Substring(0, Math.Min(50, questionText.Length))}...");
        }



        /// <summary>
        /// Reads question data from the Excel file based on questionID.
        /// Options are read from column 2 onwards, only non-empty options are read.
        /// </summary>
        public void LoadQuestionFromExcel()
        {
            UpdateExcelPath();
            if (string.IsNullOrEmpty(excelFilePath) || !File.Exists(excelFilePath))
            {
                Debug.LogError("Excel file not found at: " + excelFilePath);
                return;
            }

            using (FileStream stream = new FileStream(excelFilePath, FileMode.Open, FileAccess.Read))
            {
                IWorkbook workbook = new XSSFWorkbook(stream);
                ISheet sheet = workbook.GetSheetAt(0);
                bool found = false;

                // Skip header (row 0)
                for (int i = 1; i <= sheet.LastRowNum; i++)
                {
                    IRow row = sheet.GetRow(i);
                    if (row == null) continue;

                    // Column 0: ID
                    ICell cellId = row.GetCell(0);
                    int id = 0;
                    if (cellId != null)
                    {
                        if (cellId.CellType == CellType.Numeric)
                            id = (int)cellId.NumericCellValue;
                        else
                            int.TryParse(cellId.ToString(), out id);
                    }

                    if (id == questionID)
                    {
                        // Load Question (Column 1)
                        ICell cellQuestion = row.GetCell(1);
                        questionText = cellQuestion != null ? cellQuestion.ToString() : "";

                        // Load Options: from column 2 onwards (only read non-empty options)
                        options.Clear();
                        correctAnswer = "";
                        short lastCellNum = row.LastCellNum; // lastCellNum = number of cells (last position + 1)
                        for (int j = 2; j < lastCellNum; j++)
                        {
                            ICell cellOption = row.GetCell(j);
                            string optionStr = cellOption != null ? cellOption.ToString().Trim() : "";
                            if (string.IsNullOrEmpty(optionStr))
                                continue; // skip empty option

                            options.Add(optionStr);

                            // If the cell is green, this is the correct answer.
                            if (string.IsNullOrEmpty(correctAnswer) && IsCellGreen(cellOption))
                            {
                                correctAnswer = optionStr;
                            }
                        }

                        // If no correct answer is found, default to the first option (if any)
                        if (string.IsNullOrEmpty(correctAnswer) && options.Count > 0)
                            correctAnswer = options[0];

                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    // If the question is not found, clear the content to display an error HelpBox.
                    questionText = "";
                    options.Clear();
                    correctAnswer = "";
                }
            }
            // ShuffleOptions(); // Bỏ shuffle để giữ thứ tự câu trả lời

            // Update content to UI Text.
            UpdateTextFields();
        }

        /// <summary>
        /// Updates the TextMeshProUGUIs with the loaded data.
        /// </summary>
        public void UpdateTextFields()
        {
            if (_tmpQuestion != null)
                _tmpQuestion.text = questionText;

            if (_lstOptions != null && _lstOptions.Count > 0)
            {
                // Only updates the available UI Text elements (may be fewer than the number of options if the UI isn't fully set up).
                for (int i = 0; i < _lstOptions.Count && i < options.Count; i++)
                {
                    _lstOptions[i].text = options[i];
                }
            }
        }

        /// <summary>
        /// Overwrites the current data (questionText, options, correctAnswer) to the Excel file.
        /// After updating, sets updateStatusMessage and updateStatusSuccess to display a HelpBox on the Inspector.
        /// When adding a new option, if the cell doesn't exist, it copies the style from the previous option (if available).
        /// </summary>
        public void UpdateExcel()
        {
            try
            {
                // Retrieves the excelFilePath from PlayerPrefs (in case it has changed).
                excelFilePath = PlayerPrefs.GetString("SelectedExcelPath", "");

                if (string.IsNullOrEmpty(excelFilePath) || !File.Exists(excelFilePath))
                {
                    updateStatusMessage = "Update failed !!! File not found: " + excelFilePath;
                    updateStatusSuccess = false;
                    return;
                }

                // --- STEP 1: Read the workbook from the Excel file ---
                IWorkbook workbook;
                using (FileStream readStream = new FileStream(excelFilePath, FileMode.Open, FileAccess.Read))
                {
                    workbook = new XSSFWorkbook(readStream);
                }

                ISheet sheet = workbook.GetSheetAt(0);
                bool found = false;

                for (int i = 1; i <= sheet.LastRowNum; i++)
                {
                    IRow row = sheet.GetRow(i);
                    if (row == null) continue;

                    ICell cellId = row.GetCell(0);
                    int id = 0;
                    if (cellId != null)
                    {
                        if (cellId.CellType == CellType.Numeric)
                            id = (int)cellId.NumericCellValue;
                        else
                            int.TryParse(cellId.ToString(), out id);
                    }

                    if (id == questionID)
                    {
                        // Update Question (Column 1)
                        ICell cellQuestion = row.GetCell(1) ?? row.CreateCell(1);
                        cellQuestion.SetCellValue(questionText);

                        // Update Options: start from column 2 according to the number of options in the list.
                        for (int j = 0; j < options.Count; j++)
                        {
                            int cellIndex = j + 2;
                            ICell cellOption = row.GetCell(cellIndex);
                            // If the cell doesn't exist (new option), create the cell and copy the style from the previous cell (if available).
                            if (cellOption == null)
                            {
                                cellOption = row.CreateCell(cellIndex);
                                if (j > 0)
                                {
                                    ICell previousCell = row.GetCell(cellIndex - 1);
                                    if (previousCell != null && previousCell.CellStyle != null)
                                    {
                                        cellOption.CellStyle = previousCell.CellStyle;
                                    }
                                }
                            }

                            cellOption.SetCellValue(options[j]);

                            // If this option is the correct answer, update the fill color to green while preserving the existing style/font properties.
                            if (options[j] == correctAnswer)
                            {
                                ICellStyle oldStyle = cellOption.CellStyle;
                                ICellStyle newStyle = workbook.CreateCellStyle();
                                if (oldStyle != null)
                                {
                                    newStyle.CloneStyleFrom(oldStyle);
                                }

                                // Create XSSFColor without the second parameter.
                                XSSFColor greenColor = new XSSFColor(new byte[] { 0, 255, 0 });
                                ((XSSFCellStyle)newStyle).SetFillForegroundColor(greenColor);
                                newStyle.FillPattern = FillPattern.SolidForeground;
                                cellOption.CellStyle = newStyle;
                            }
                        }

                        // If the Excel file has more option columns than the current number of options, clear the contents of the extra cells (if necessary).
                        short lastCellNum = row.LastCellNum;
                        if (lastCellNum > options.Count + 2)
                        {
                            for (int j = options.Count + 2; j < lastCellNum; j++)
                            {
                                ICell extraCell = row.GetCell(j);
                                if (extraCell != null)
                                    extraCell.SetCellValue(string.Empty);
                            }
                        }

                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    updateStatusMessage = "Update failed !!! Question ID does not exist in the Excel file.";
                    updateStatusSuccess = false;
                    return;
                }

                // --- STEP 2: Write the updated workbook back to the Excel file ---
                using (FileStream writeStream = new FileStream(excelFilePath, FileMode.Create, FileAccess.Write))
                {
                    workbook.Write(writeStream);
                }

                updateStatusMessage = "Update Ok !";
                updateStatusSuccess = true;
            }
            catch (Exception ex)
            {
                updateStatusMessage = "Update failed !!! " + ex.Message;
                updateStatusSuccess = false;
            }
        }

        /// <summary>
        /// Checks if the cell has a green background color (RGB: 0, 255, 0).
        /// (Only applicable to .xlsx files using NPOI.XSSF)
        /// </summary>
        /// <param name="cell">The cell to check</param>
        /// <returns>True if the cell is green, false otherwise</returns>
        private bool IsCellGreen(ICell cell)
        {
            if (cell == null || cell.CellStyle == null)
                return false;

            var xCellStyle = cell.CellStyle as XSSFCellStyle;
            if (xCellStyle == null)
                return false;

            var color = xCellStyle.FillForegroundColorColor as XSSFColor;
            if (color == null)
                return false;

            byte[] rgb = color.RGB;
            if (rgb == null || rgb.Length < 3)
                return false;

            return (rgb[0] == 0 && rgb[1] == 255 && rgb[2] == 0);
        }

        /// <summary>
        /// Reads the Excel file to count the total number of questions.
        /// Iterates from row 1 (skipping the header in row 0) and stops if the cell in the Question ID column (column 0) is empty.
        /// </summary>
        public int GetTotalQuestions()
        {
            // API mode - lấy từ cache
            if (quizDatabase != null && quizDatabase.DataMode == QuizDataMode.API)
            {
                if (cachedAPIQuestions == null)
                {
                    CacheAPIQuestions();
                }
                return cachedAPIQuestions?.Count ?? 0;
            }

            // Excel mode
            string path = excelFilePath;
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return 0;
            using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read))
            {
                IWorkbook workbook = new XSSFWorkbook(fs);
                ISheet sheet = workbook.GetSheetAt(0);
                int count = 0;
                // Iterate from row 1 (skipping the header)
                for (int i = 1; i <= sheet.LastRowNum; i++)
                {
                    IRow row = sheet.GetRow(i);
                    if (row == null)
                        break;
                    ICell cell = row.GetCell(0);
                    // If the cell in the Question ID column is empty, stop.
                    if (cell == null || string.IsNullOrEmpty(cell.ToString().Trim()))
                        break;
                    count++;
                }

                return count;
            }
        }


        public int correctIndexAfterShuffle { get; private set; }

        private void ShuffleOptions()
        {
            if (options.Count <= 1) return;

            string correctValue = correctAnswer;

            for (int i = options.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (options[i], options[j]) = (options[j], options[i]);
            }

            correctIndexAfterShuffle = options.IndexOf(correctValue);
        }
    }
}