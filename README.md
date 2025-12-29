# E-Commerce Web Application (ASP.NET Razor Pages)

## 📌 Project Overview

This project is a **modular e-commerce web application** built using **ASP.NET Core Razor Pages**, **Entity Framework Core**, **SQL Server**, and **Redis**. The architecture emphasizes **clean separation of concerns**, **performance optimization**, and **scalability**, particularly through the use of **DTOs** and **Redis caching**.

The application supports product browsing, cart management, user authentication via JWT, and administrative CRUD operations.

---

## 🧱 Tech Stack

* **ASP.NET Core Razor Pages** – UI & request handling
* **Entity Framework Core** – ORM for database access
* **SQL Server** – Primary relational database
* **Redis (Standalone Mode)** – High-performance caching layer
* **StackExchange.Redis** – Redis client
* **JWT (JSON Web Tokens)** – Authentication & authorization
* **JavaScript (Fetch API)** – Client-side interactions

---

## 📂 Project Structure

```
Pages/
├── Index.cshtml.cs
├── Cart.cshtml.cs
├── login/
│   ├── Login.cshtml.cs
│   ├── Logout.cshtml.cs
│   └── createAccount.cshtml.cs
├── produit/
│   ├── MyListedProducts.cshtml.cs
│   ├── Details.cshtml.cs
│   ├── Create.cshtml.cs
│   ├── Edit.cshtml.cs
│   └── Delete.cshtml.cs
└── user/
    |── Details.cshtml.cs
    ├── Create.cshtml.cs
    ├── Edit.cshtml.cs
    └── Delete.cshtml.cs
    
```

---

## 📄 Pages Overview

### 🔐 `Pages/login`

Handles **user authentication and account management** using JWT:

* User login
* User logout
* Account creation (registration)
* JWT stored securely in HTTP-only cookies

---

### 🛒 `Pages/Cart.cshtml.cs`

Contains **all shopping cart logic**:

* Cart stored in **Redis** (keyed by GuestId or UserId)
* Cart data structure: `Dictionary<int, int>` (`ProductId → Quantity`)
* Server-side validation to prevent request tampering

---

### 🏠 `Pages/Index.cshtml.cs`

Main application entry point:

* Displays product listings
* Category, price, and stock filtering
* Handles **Add to Cart** operations
* Sends lightweight **Product Preview DTOs** to Redis on product click

---

### 📦 `Pages/produit`

Manages **product lifecycle and details**:

* Create products
* Read product details
* Update product information
* Delete products

#### Product Details Optimization

* Uses **read-through Redis cache**
* On cache miss:

  * Loads product from database
  * Serializes and stores it in Redis with TTL
* Prevents unnecessary database access

---

### 👤 `Pages/user`

Handles **user account management**:

* CRUD operations for user data
* Account updates

📌 *Planned integration with `Pages/login/Details` for unified user management.*

---

## 🚀 Caching Strategy (Redis)

### 🧠 Why Redis?

* Extremely fast (in-memory)
* Reduces database load
* Ideal for read-heavy operations (product views)

### 🔑 Cache Usage

| Use Case            | Redis Key Pattern                | TTL            |
| ------------------- | -------------------------------- | -------------- |
| Cart                | `Cart:{GuestId}`                 | Session-based  |
| Product Preview     | `ProductPreview:{ProductId}`     | Short (≈5 min) |

### 📦 DTO-Based Design

* **DTOs** are used instead of EF entities
* Prevents over-fetching
* Avoids EF tracking issues
* Ensures serialization safety

---

## 🔄 Read-Through Cache Flow (Product Details)

1. User clicks product on Index page
2. Lightweight DTO is sent via JavaScript
3. DTO is serialized into Redis
4. Details page:

   * Attempts Redis read
   * On miss → queries DB → updates Redis
5. Page renders without unnecessary DB access

---

## 🔐 Security Considerations

* JWT stored in **HTTP-only cookies**
* Server-side validation of quantities and prices
* No trust in client-submitted data
* Redis used only for **non-sensitive** cached data

---

## 📈 Performance & Benchmarking

Key performance indicators monitored:

* Latency (cache hit vs DB hit)
* Throughput (requests/sec)
* Cache hit ratio
* Redis memory usage
* Eviction rates

Tools used:

* `redis-cli`
* Web Dev Tools

---

## 🛠️ Development Notes

* Redis runs in **standalone mode**
* Recommended setup: Redis on Windows for local development
* WSL Redis supported with correct network binding

---

## 🧭 Future Improvements and Updates

* Implemeting a Shopping Assistant Agent with RAG 
* Adding a Read-Through cache based on the most viewed/trending products to minimize database read operations
* Payment Processing (see next section)
* Add performance measures

---
## 💳 Payment Processing (To add)

### Concurrency Control & Idempotent Order Placement Using Redis

This module ensures **safe, consistent, and duplicate-free order processing** in concurrent environments by combining **Redis-based locking** with **idempotency keys**.

### 🔐 Objectives

* Prevent double charges
* Avoid duplicate orders
* Handle network retries safely
* Guarantee exactly-once order execution

---

### 🧠 Core Concepts

#### 1️⃣ Idempotency Keys

Each payment request includes a **unique idempotency key** generated by the client or server.

* Same request + same key → same result
* Prevents duplicate orders caused by retries or client-side resubmissions

**Redis key pattern:**

```
Idempotency:Order:{UserId}:{Key}
```

---

#### 2️⃣ Distributed Locking (Concurrency Control)

Redis is used to enforce **single-writer access** during order placement.

**Redis lock key:**

```
Lock:Order:{UserId}
```

* Ensures only one order placement runs at a time per user
* Prevents race conditions under concurrent requests

---

### 🔄 Order Placement Flow

1. Client initiates payment request
2. Server checks idempotency key in Redis

   * If exists → return cached response
3. Acquire Redis lock using `SET NX EX`
4. Validate cart and inventory
5. Persist order to database
6. Cache order result using idempotency key (with TTL)
7. Release Redis lock
8. Return order confirmation

---

### 🧩 Redis Commands Used

| Purpose           | Redis Command          |
| ----------------- | ---------------------- |
| Acquire lock      | `SET lock value NX EX` |
| Idempotency check | `GET key`              |
| Persist result    | `SET key value EX`     |
| Cleanup           | `DEL lock`             |

---

### ⏱️ TTL Strategy

| Key Type        | TTL                    |
| --------------- | ---------------------- |
| Lock            | Short (5–10 seconds)   |
| Idempotency Key | Medium (10–30 minutes) |

---

### ⚠️ Failure Handling

* Lock TTL prevents deadlocks
* Duplicate requests return cached responses
* Partial failures do not create duplicate orders

---

### 🏁 Outcome

This design guarantees:

* Exactly-once payment execution
* Concurrency-safe order placement
* Robust handling of retries and network failures

---

## ✅ Summary

This project demonstrates a **production-oriented Razor Pages architecture** using:

* Clean separation of responsibilities
* DTO-driven communication
* Redis-powered performance optimizations
* Secure JWT authentication

Designed for **scalability, clarity, and maintainability**.

