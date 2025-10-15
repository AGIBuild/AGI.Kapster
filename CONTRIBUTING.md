# Contributing Guide

Thank you for your interest in AGI.Kapster! We welcome all forms of contributions.

## 🚀 Quick Start

### Development Setup

1. **Clone Repository**
   ```bash
   git clone https://github.com/AGIBuild/AGI.Kapster.git
   cd AGI.Kapster
   ```

2. **Install Dependencies**
- .NET 9.0 SDK
- Visual Studio 2022 / JetBrains Rider / VS Code

3. **Build and Test**
   ```bash
   dotnet restore
   ./build.ps1
   ```

4. **Run Application**
   ```bash
   dotnet run --project src/AGI.Kapster.Desktop
   ```

## 🛠️ Development

### Project Structure

```
AGI.Kapster/
├── src/AGI.Kapster.Desktop/     # Main application
├── tests/AGI.Kapster.Tests/     # Test projects
├── build/                      # Build scripts
├── packaging/                  # Package creation scripts
└── docs/                      # Documentation
```

### Key Components

- **Models/**: Data models and DTOs
- **Services/**: Business logic and platform abstractions
- **ViewModels/**: MVVM view models
- **Views/**: UI definitions (XAML)
- **Overlays/**: Screenshot overlay windows
- **Commands/**: Undo/redo functionality
- **Rendering/**: Graphics rendering

### Architecture

- **MVVM Pattern**: Using CommunityToolkit.Mvvm
- **Dependency Injection**: Microsoft.Extensions.DependencyInjection
- **Cross-platform UI**: Avalonia UI 11.x
- **Graphics**: SkiaSharp for image processing
- **Logging**: Serilog for structured logging

## 📝 Contributing

### Types of Contributions

- 🐛 **Bug Reports**: Report issues with detailed reproduction steps
- 💡 **Feature Requests**: Suggest new features or improvements
- 📖 **Documentation**: Improve existing documentation
- 💻 **Code Contributions**: Submit pull requests
- 🌍 **Localization**: Add or improve translations

### Development Workflow

1. **Fork Repository**: Create your own fork
2. **Create Branch**: `git checkout -b feature/your-feature-name`
3. **Make Changes**: Implement your changes
4. **Run Tests**: `./build.ps1 Test`
5. **Commit Changes**: Use conventional commit format
6. **Push Changes**: `git push origin feature/your-feature-name`
7. **Create Pull Request**: Submit PR with description

### Code Standards

#### C# Coding Standards
- Follow C# naming conventions
- Use meaningful variable and method names
- Add XML documentation for public APIs
- Prefer synchronous APIs when the codebase uses sync for overlay control
  and use async/await where naturally asynchronous (I/O, network)
- Handle exceptions appropriately

#### Commit Message Format
Use conventional commits:
```
feat: add new annotation tool
fix: resolve hotkey registration issue
docs: update installation guide
refactor: simplify overlay management
```

#### Code Style
- Use 4 spaces for indentation
- Use PascalCase for public members
- Use camelCase for private members
- Use meaningful names for variables and methods

### Testing

#### Running Tests
```bash
# Run all tests
./build.ps1 Test

# Run with coverage
./build.ps1 Test -Coverage

# Run specific test
dotnet test tests/AGI.Kapster.Tests --filter "FullyQualifiedName~HotkeyManagerTests"
```

#### Test Requirements
- **Unit Tests**: Test individual components
- **Integration Tests**: Test component interactions
- **Coverage**: Maintain 80%+ code coverage
- **Mocking**: Use mock services for dependencies

### Pull Request Guidelines

#### Before Submitting
- [ ] Code follows project standards
- [ ] Tests pass and coverage is maintained
- [ ] Documentation is updated if needed
- [ ] Commit messages follow conventional format

#### PR Description
Include:
- **Summary**: Brief description of changes
- **Type**: Bug fix, feature, documentation, etc.
- **Testing**: How changes were tested
- **Breaking Changes**: Any breaking changes

#### Review Process
- All PRs require review
- Address feedback promptly
- Keep PRs focused and small
- Update documentation as needed

## 🐛 Bug Reports

### Before Reporting
- Check existing issues
- Try latest version
- Search documentation

### Bug Report Template
```markdown
**Bug Description**
Brief description of the bug

**Steps to Reproduce**
1. Step one
2. Step two
3. Step three

**Expected Behavior**
What should happen

**Actual Behavior**
What actually happens

**Environment**
- OS: Windows 10/macOS 12/Ubuntu 20.04
- Version: v1.0.0
- Architecture: x64/ARM64

**Additional Context**
Any other relevant information
```

## 💡 Feature Requests

### Before Requesting
- Check existing feature requests
- Consider if feature fits project scope
- Think about implementation complexity

### Feature Request Template
```markdown
**Feature Description**
Brief description of the feature

**Use Case**
Why is this feature needed?

**Proposed Solution**
How should this feature work?

**Alternatives**
Other ways to achieve the same goal

**Additional Context**
Any other relevant information
```

## 📖 Documentation

### Documentation Types
- **API Documentation**: XML comments for public APIs
- **User Documentation**: README, user guides
- **Developer Documentation**: Architecture, setup guides
- **Code Comments**: Inline code documentation

### Documentation Standards
- Use clear, concise language
- Include code examples
- Keep documentation up-to-date
- Use consistent formatting

## 🌍 Localization

### Adding Translations
1. Create translation files in appropriate directories
2. Follow existing naming conventions
3. Test translations in target locale
4. Update documentation for new languages

### Translation Guidelines
- Use professional translation services
- Test with native speakers
- Maintain consistency with existing translations
- Update when source text changes

## 📄 License

By contributing to AGI.Kapster, you agree that your contributions will be licensed under the MIT License.

## 🤝 Community

### Getting Help
- **GitHub Issues**: For bugs and feature requests
- **Discussions**: For questions and general discussion
- **Documentation**: Check existing docs first

### Code of Conduct
- Be respectful and inclusive
- Focus on constructive feedback
- Help others learn and grow
- Follow GitHub's community guidelines

---

Thank you for contributing to AGI.Kapster! 🚀