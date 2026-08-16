# Forge Project Overview

## Project

Forge is an enterprise-grade Lost & Found platform built using:

- ABP Framework
- .NET 10
- Modular Monolith Architecture

The project is designed to provide a scalable platform for reporting, managing and matching lost and found items using modern AI technologies.

---

# Architecture

The solution follows ABP Modular Monolith principles.

Each module is responsible for its own domain.

The LostFound module is one of the business modules inside the solution.

The architecture emphasizes:

- Clean Architecture
- SOLID
- Dependency Injection
- Domain-driven design
- High cohesion
- Low coupling
- Extensibility
- Maintainability

---

# Main Modules

Examples include:

- LostFound
- Identity / IAM
- Administration
- Shared Infrastructure
- Background Processing
- Reporting
- Analytics

Each module should remain isolated whenever possible.

---

# LostFound Module

The LostFound module is responsible for:

- Lost reports
- Found reports
- Reporter management
- Categories
- Locations
- AI-assisted matching
- Semantic search
- Notifications
- Background matching jobs
- Search ranking

This module is the primary focus of the current development effort.

---

# AI Layer

The AI subsystem currently supports multiple providers including:

- Gemini
- OpenAI
- Ollama
- HuggingFace
- DeepSeek

Current provider responsibilities include:

- Classification
- Embeddings
- Semantic matching
- AI search

The long-term goal is to reduce dependency on external providers.

---

# Development Principles

Always prioritize:

- Clean Architecture
- Maintainability
- Performance
- Backward compatibility
- Production readiness

Avoid unnecessary redesigns outside the current module.

---

# Repository Structure

Workspace Root:

C:\Users\Windows 11\Desktop\Forge

Current implementation target:

modules/lostfound/src

The entire solution may be inspected for architectural understanding.

Implementation changes should remain focused on the LostFound module unless minimal integration changes are required.

---

# Build Verification

Every significant implementation should be validated using:

- dotnet restore
- dotnet build
- dotnet run
- dotnet test (when applicable)

Never leave the solution in a broken state.

---

# Documentation

The Semantic AI redesign documentation is located under:

docs/Semantic-AI/

Implementation must follow the assigned Phase and Part.