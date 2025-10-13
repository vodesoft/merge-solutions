using MergeSolutions.Core;
using MergeSolutions.Core.Models;
using MergeSolutions.Core.Parsers;
using MergeSolutions.Core.Services;

// ReSharper disable LocalizableElement

namespace MergeSolutions.UI
{
    public partial class MainForm : Form
    {
        public const string MergePlanExtension = ".msp1";

        public const string MergeSolutionsPlanFilter =
            $"MergeSolutions Plan ({MergePlanExtension})|*{MergePlanExtension}|All files (*.*)|*.*";

        public const string SolutionFilter =
            "Visual Studio Solution (.sln)|*.sln|All files (*.*)|*.*";

        public const string AppName = "Merge Solutions UI";

        private readonly IMergeSolutionsService _mergeSolutionsService;

        private readonly IMigrator _migrator;

        private readonly ISolutionService _solutionService;

        private MergePlan _mergePlan = new();

        public MainForm(IMergeSolutionsService mergeSolutionsService, ISolutionService solutionService, IStartup startup,
            IMigrator migrator)
        {
            _mergeSolutionsService = mergeSolutionsService;
            _solutionService = solutionService;
            _migrator = migrator;
            InitializeComponent();
            if (startup.PlanPath != null)
            {
                LoadMergePlan(startup.PlanPath);
            }
        }

        private void buttonAppendSolution_Click(object sender, EventArgs e)
        {
            InTryCatch(() =>
            {
                UiToPlan();
                using var openFileDialog = new OpenFileDialog
                {
                    Filter = SolutionFilter,
                    FilterIndex = 1,
                    InitialDirectory = _mergePlan.RootDir
                };

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    var stream = openFileDialog.OpenFile();
                    using (stream)
                    {
                        _mergePlan.AddSolution(openFileDialog.FileName, _solutionService);
                        PlanToUi();
                    }
                }
            });
        }

        private void buttonOpenPlan_Click(object sender, EventArgs e)
        {
            InTryCatch(() =>
            {
                using var openFileDialog = new OpenFileDialog
                {
                    Filter = MergeSolutionsPlanFilter,
                    FilterIndex = 1,
                    InitialDirectory = _mergePlan.RootDir,
                };

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    var fileName = openFileDialog.FileName;
                    LoadMergePlan(fileName);
                }
            });
        }

        private void buttonReset_Click(object sender, EventArgs e)
        {
            InTryCatch(() =>
            {
                _mergePlan = new MergePlan();
                PlanToUi();
            });
        }

        private void buttonRunMerge_Click(object sender, EventArgs e)
        {
            InTryCatch(() =>
            {
                if (_mergePlan.RootDir == null)
                {
                    throw new InvalidOperationException(
                        "No root directory known. Please save merge plan first to resolve relative paths.");
                }

                using var saveFileDialog = new SaveFileDialog
                {
                    Filter = SolutionFilter,
                    FilterIndex = 1,
                    FileName = _mergePlan.OutputSolutionPath,
                    InitialDirectory = _mergePlan.RootDir,
                };

                if (saveFileDialog.ShowDialog() == DialogResult.OK)
                {
                    UiToPlan();
                    _mergePlan.OutputSolutionPath = saveFileDialog.FileName;
                    _mergeSolutionsService.MergeSolutions(_mergePlan);
                    PlanToUi();
                    MessageBox.Show("Merged solution is created.", AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            });
        }

        private void buttonSaveMergePlan_Click(object sender, EventArgs e)
        {
            InTryCatch(() =>
            {
                UiToPlan();
                using var saveFileDialog = new SaveFileDialog
                {
                    Filter = MergeSolutionsPlanFilter,
                    FilterIndex = 1,
                    FileName = $"{Path.GetFileNameWithoutExtension(_mergePlan.PlanName)}{MergePlanExtension}",
                    InitialDirectory = _mergePlan.RootDir,
                };

                if (saveFileDialog.ShowDialog() == DialogResult.OK)
                {
                    _mergePlan.SaveMergePlanFile(saveFileDialog.FileName);
                    PlanToUi();
                }
            });
        }

        private void InTryCatch(Action action, string? message = null, string? caption = null)
        {
            try
            {
                Enabled = false;
                Update();
                action();
            }
            catch (Exception e)
            {
                Program.ShowExceptionMessage(message ?? "Operation failed", e, caption ?? "Operation failed");
            }
            finally
            {
                Enabled = true;
                Update();
            }
        }

        private void LoadMergePlan(string fileName)
        {
            _mergePlan = MergePlan.LoadMergePlanFile(fileName, _migrator);
            PlanToUi();
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            PlanToUi();
        }

        private void PlanToUi()
        {
            Text = $"{AppName} : {_mergePlan.PlanName}";
            labelMergedSolutionPath.Text = _mergePlan.OutputSolutionPath;
            labelMergePlanPath.Text = _mergePlan.RootDir == null || _mergePlan.PlanName == null
                ? ""
                : $"Root dir: {_mergePlan.RootDir}";
            try
            {
                treeViewSolutions.BeginUpdate();
                treeViewSolutions.Nodes.Clear();
                treeViewSolutions.CheckBoxes = true;
                foreach (var solutionEntity in _mergePlan.Solutions.OrderBy(s => s.NodeName).ToArray())
                {
                    if (solutionEntity.RelativePath == null)
                    {
                        continue;
                    }

                    SolutionInfo solutionInfo;
                    try
                    {
                        solutionInfo = _solutionService.ParseSolution(solutionEntity.RelativePath, _mergePlan.RootDir);
                    }
                    catch (DirectoryNotFoundException e)
                    {
                        Program.ShowExceptionMessage($"Warning: {solutionEntity.NodeName} is removed from the merge plan.", e,
                            $"Cannot load solution {solutionEntity.NodeName}");
                        _mergePlan.Solutions.Remove(solutionEntity);
                        continue;
                    }

                    solutionEntity.NodeName ??= solutionInfo.Name;
                    var solutionTreeNode = new SolutionTreeNode(solutionEntity);

                    solutionTreeNode.ContextMenuStrip = new ContextMenuStrip
                    {
                        Items =
                        {
                            {
                                "Rename", null, (_, _) =>
                                {
                                    treeViewSolutions.LabelEdit = true;
                                    solutionTreeNode.Text = solutionTreeNode.SolutionEntity.NodeName;
                                    solutionTreeNode.BeginEdit();
                                }
                            },
                            {
                                "Delete", null, (_, _) =>
                                {
                                    _mergePlan.Solutions.RemoveAll(s => s == solutionTreeNode.SolutionEntity);
                                    PlanToUi();
                                }
                            }
                        }
                    };

                    var hasChecked = false;
                    foreach (var project in solutionInfo.Projects.OfType<Project>().OrderBy(p => p.Name))
                    {
                        var projectNode = new TreeNode(project.Name)
                        {
                            Checked = !_mergePlan.IsExcluded(project),
                            Tag = project.Guid
                        };

                        solutionTreeNode.Nodes.Add(projectNode);
                        hasChecked |= projectNode.Checked;
                    }

                    solutionTreeNode.Checked = hasChecked;

                    treeViewSolutions.Nodes.Add(solutionTreeNode);
                    if (!solutionEntity.Collapsed)
                    {
                        solutionTreeNode.Expand();
                    }
                }
            }
            finally
            {
                treeViewSolutions.EndUpdate();
            }
        }

        private void treeViewSolutions_AfterCheck(object sender, TreeViewEventArgs e)
        {
            foreach (TreeNode nodeNode in e.Node!.Nodes)
            {
                nodeNode.Checked = e.Node.Checked;
            }
        }

        private void treeViewSolutions_AfterLabelEdit(object sender, NodeLabelEditEventArgs e)
        {
            e.CancelEdit = true;
            if (e.Node is SolutionTreeNode solutionTreeNode)
            {
                var nodeText = e.Label ?? solutionTreeNode.NodeName;
                solutionTreeNode.NodeName = nodeText;
            }
        }

        private void UiToPlan()
        {
            var excludedProjects = new List<KeyValuePair<string, string>>();
            foreach (SolutionTreeNode solutionTreeNode in treeViewSolutions.Nodes)
            {
                solutionTreeNode.SolutionEntity.NodeName = solutionTreeNode.NodeName;
                solutionTreeNode.SolutionEntity.Collapsed = !solutionTreeNode.IsExpanded;

                foreach (TreeNode projectNode in solutionTreeNode.Nodes)
                {
                    if (!projectNode.Checked)
                    {
                        excludedProjects.Add(new KeyValuePair<string, string>(solutionTreeNode.SolutionEntity.RelativePath!,
                            (string)projectNode.Tag));
                    }
                }
            }

            _mergePlan.ExcludedProjects = excludedProjects.ToArray();
        }
    }
}