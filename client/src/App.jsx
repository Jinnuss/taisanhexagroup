import React, { useEffect, useRef, useState } from "react";
import {
  createAsset, deleteAsset, downloadImportTemplate, exportAssets,
  getAssets, getAssetStats, importAssets, updateAsset
} from "./services/assetService";
import { getDepartments } from "./services/departmentService";
import { getCategories } from "./services/categoryService";
import { getStoredUser, getCurrentUser, logout } from "./services/authService";
import Login from "./Login";
import DepartmentManager from "./DepartmentManager";
import CategoryManager from "./CategoryManager";
import UserManager from "./UserManager";
import { assignAsset, getAssigneeSuggestions, getAssetHistory, returnAsset } from "./services/assignmentService";
import {
  addSoftware, deleteAssetImage, deleteSoftware, getImages, getQr, getSoftware,
  setPrimaryImage, uploadImages
} from "./services/qrSoftwareService";

const emptyForm = {
  assetCode: "", assetName: "", categoryId: "", departmentId: "",
  assignedTo: "", ipAddress: "", serialNumber: "", purchaseDate: "",
  purchasePrice: "", status: "available", description: ""
};

const statusNames = {
  available: "Có sẵn", using: "Đã sử dụng", maintenance: "Bảo trì",
  broken: "Đã hỏng", liquidated: "Đã thanh lý"
};

function App() {
  const [currentUser, setCurrentUser] = useState(getStoredUser());
  const [currentPage, setCurrentPage] = useState("assets");
  const [assets, setAssets] = useState([]);
  const [departments, setDepartments] = useState([]);
  const [categories, setCategories] = useState([]);
  const [stats, setStats] = useState({ overall: { Total: 0, InUse: 0, OtherCount: 0 }, departments: [] });
  const [filters, setFilters] = useState({ search: "", departmentId: "", categoryId: "", status: "" });
  const [debouncedSearch, setDebouncedSearch] = useState("");
  const [pagination, setPagination] = useState({ page: 1, pageSize: 10, total: 0, totalPages: 1 });
  const [form, setForm] = useState(emptyForm);
  const [editingId, setEditingId] = useState(null);
  const [isAssetModalOpen, setIsAssetModalOpen] = useState(false);
  const [formErrors, setFormErrors] = useState([]);
  const [message, setMessage] = useState("");
  const [errorDetails, setErrorDetails] = useState([]);
  const [loading, setLoading] = useState(false);
  const [excelLoading, setExcelLoading] = useState(false);
  const [assignmentModal, setAssignmentModal] = useState(false);
  const [historyModal, setHistoryModal] = useState(false);
  const [activeAsset, setActiveAsset] = useState(null);
  const [assigneeSuggestions, setAssigneeSuggestions] = useState([]);
  const [historyData, setHistoryData] = useState({ history: [], assignments: [] });
  const [assignmentForm, setAssignmentForm] = useState({
    userId: "", assignedTo: "", date: new Date().toISOString().slice(0, 10), note: ""
  });
  const [qrModal, setQrModal] = useState(false);
  const [qrData, setQrData] = useState(null);
  const [softwareModal, setSoftwareModal] = useState(false);
  const [software, setSoftware] = useState([]);
  const [softwareForm, setSoftwareForm] = useState({ softwareName: "", version: "", publisher: "", installedDate: "", notes: "" });
  const [imageModal, setImageModal] = useState(false);
  const [assetImages, setAssetImages] = useState([]);
  const [imageLoading, setImageLoading] = useState(false);
  const [imageFeedback, setImageFeedback] = useState({ type: "", text: "" });
  const fileInputRef = useRef(null);
  const imageInputRef = useRef(null);

  useEffect(() => {
    let active = true;
    const expired = () => setCurrentUser(null);
    window.addEventListener("auth-expired", expired);
    if (localStorage.getItem("accessToken")) {
      getCurrentUser().then(user => { if (active) setCurrentUser(user); }).catch(error => {
        if (active && error.response?.status === 401) setCurrentUser(null);
      });
    } else setCurrentUser(null);
    return () => { active = false; window.removeEventListener("auth-expired", expired); };
  }, []);

  useEffect(() => {
    const timer = setTimeout(() => {
      setDebouncedSearch(filters.search.trim());
      setPagination((value) => ({ ...value, page: 1 }));
    }, 350);
    return () => clearTimeout(timer);
  }, [filters.search]);

  useEffect(() => {
    if (!currentUser) return;
    Promise.all([getDepartments(), getCategories(), getAssetStats()])
      .then(([departmentData, categoryData, statsData]) => {
        setDepartments(departmentData);
        setCategories(categoryData);
        setStats(statsData);
      })
      .catch(showError);
  }, [currentUser]);

  useEffect(() => {
    if (currentUser && currentPage === "assets") loadAssets();
  }, [currentUser, currentPage, debouncedSearch, filters.departmentId, filters.categoryId, filters.status, pagination.page, pagination.pageSize]);

  useEffect(() => {
    function escape(event) {
      if (event.key === "Escape") setIsAssetModalOpen(false);
    }
    window.addEventListener("keydown", escape);
    return () => window.removeEventListener("keydown", escape);
  }, []);

  function showError(error) {
    setMessage(error?.response?.data?.message || error?.message || "Đã xảy ra lỗi.");
    setErrorDetails(error?.response?.data?.errors || []);
  }

  function queryParams() {
    return {
      search: debouncedSearch || undefined,
      departmentId: filters.departmentId || undefined,
      categoryId: filters.categoryId || undefined,
      status: filters.status || undefined,
      page: pagination.page,
      pageSize: pagination.pageSize
    };
  }

  async function loadAssets() {
    try {
      setLoading(true);
      const data = await getAssets(queryParams());
      setAssets(data.items);
      setPagination(data.pagination);
    } catch (error) {
      showError(error);
    } finally {
      setLoading(false);
    }
  }

  async function refreshAll(successMessage = "") {
    try {
      const [assetData, statsData, departmentData, categoryData] = await Promise.all([
        getAssets(queryParams()), getAssetStats(), getDepartments(), getCategories()
      ]);
      setAssets(assetData.items);
      setPagination(assetData.pagination);
      setStats(statsData);
      setDepartments(departmentData);
      setCategories(categoryData);
      if (successMessage) setMessage(successMessage);
    } catch (error) {
      showError(error);
    }
  }

  function updateFilter(name, value) {
    setFilters((current) => ({ ...current, [name]: value }));
    if (name !== "search") setPagination((current) => ({ ...current, page: 1 }));
  }

  function resetFilters() {
    setFilters({ search: "", departmentId: "", categoryId: "", status: "" });
    setDebouncedSearch("");
    setPagination((current) => ({ ...current, page: 1 }));
  }

  function handleChange(event) {
    const { name, value } = event.target;
    setForm((current) => ({ ...current, [name]: value }));
  }

  function payload() {
    return {
      assetCode: form.assetCode.trim(), assetName: form.assetName.trim(),
      categoryId: form.categoryId ? Number(form.categoryId) : null,
      departmentId: form.departmentId ? Number(form.departmentId) : null,
      assignedTo: form.assignedTo.trim() || null, ipAddress: form.ipAddress.trim() || null,
      serialNumber: form.serialNumber.trim() || null, purchaseDate: form.purchaseDate || null,
      purchasePrice: form.purchasePrice === "" ? null : Number(form.purchasePrice),
      status: form.status, description: form.description.trim() || null
    };
  }

  function validateAssetForm() {
    const errors = [];
    const code = form.assetCode.trim();
    if (!code) errors.push("Mã tài sản là bắt buộc.");
    else if (!/^[A-Za-z0-9._/-]+$/.test(code)) errors.push("Mã tài sản chỉ gồm chữ không dấu, số và . _ / -.");
    if (!form.assetName.trim()) errors.push("Tên tài sản là bắt buộc.");
    if (form.ipAddress && !/^((25[0-5]|2[0-4]\d|1?\d?\d)(\.|$)){4}$/.test(form.ipAddress.trim())) errors.push("Địa chỉ IPv4 không hợp lệ.");
    if (form.purchasePrice !== "" && (!Number.isFinite(Number(form.purchasePrice)) || Number(form.purchasePrice) < 0)) errors.push("Giá mua phải là số không âm.");
    if (form.description.length > 1000) errors.push("Ghi chú không được vượt quá 1000 ký tự.");
    return errors;
  }

  async function handleSubmit(event) {
    event.preventDefault();
    const clientErrors = validateAssetForm();
    if (clientErrors.length) {
      setFormErrors(clientErrors);
      return;
    }
    try {
      setFormErrors([]);
      if (editingId) await updateAsset(editingId, payload());
      else await createAsset(payload());
      const success = editingId ? "Cập nhật tài sản thành công." : "Thêm tài sản thành công.";
      closeModal();
      await refreshAll(success);
    } catch (error) {
      const serverErrors = error?.response?.data?.errors;
      setFormErrors(Array.isArray(serverErrors) ? serverErrors.map((item) => item.message || (Array.isArray(item.errors) ? item.errors.join("; ") : String(item))) : [error?.response?.data?.message || "Không thể lưu tài sản."]);
    }
  }

  function openCreate() {
    setEditingId(null);
    setForm(emptyForm);
    setIsAssetModalOpen(true);
    setFormErrors([]);
    setMessage("");
  }

  function openEdit(asset) {
    setEditingId(asset.Id);
    setForm({
      assetCode: asset.AssetCode || "", assetName: asset.AssetName || "",
      categoryId: asset.CategoryId == null ? "" : String(asset.CategoryId),
      departmentId: asset.DepartmentId == null ? "" : String(asset.DepartmentId),
      assignedTo: asset.AssignedTo || "", ipAddress: asset.IpAddress || "",
      serialNumber: asset.SerialNumber || "",
      purchaseDate: asset.PurchaseDate ? asset.PurchaseDate.substring(0, 10) : "",
      purchasePrice: asset.PurchasePrice == null ? "" : String(asset.PurchasePrice),
      status: asset.Status || "available", description: asset.Description || ""
    });
    setIsAssetModalOpen(true);
    setFormErrors([]);
    setMessage("");
  }

  function closeModal() {
    setIsAssetModalOpen(false);
    setEditingId(null);
    setForm(emptyForm);
    setFormErrors([]);
  }

  async function handleDelete(id) {
    if (!window.confirm("Bạn có chắc chắn muốn xóa tài sản này?")) return;
    try {
      await deleteAsset(id);
      await refreshAll("Xóa tài sản thành công.");
    } catch (error) {
      showError(error);
    }
  }

  async function handleExport() {
    try {
      setExcelLoading(true);
      await exportAssets(queryParams());
      setMessage("Đã xuất Excel theo bộ lọc hiện tại.");
    } catch (error) {
      showError(error);
    } finally {
      setExcelLoading(false);
    }
  }

  async function handleTemplate() {
    try {
      setExcelLoading(true);
      await downloadImportTemplate();
    } catch (error) {
      showError(error);
    } finally {
      setExcelLoading(false);
    }
  }

  function handleManagementAction(action) {
    if (!action) return;
    if (action === "departments") setCurrentPage("departments");
    else if (action === "categories") setCurrentPage("categories");
    else if (action === "users") setCurrentPage("users");
    else if (action === "template") handleTemplate();
    else if (action === "import") fileInputRef.current?.click();
    else if (action === "export") handleExport();
  }

  async function handleImport(event) {
    const file = event.target.files?.[0];
    event.target.value = "";
    if (!file) return;
    try {
      setExcelLoading(true);
      setErrorDetails([]);
      const result = await importAssets(file);
      await refreshAll(result.message);
    } catch (error) {
      showError(error);
    } finally {
      setExcelLoading(false);
    }
  }

  async function openAssignment(asset) {
    try {
      setActiveAsset(asset);
      setAssignmentForm({ userId: "", assignedTo: "", date: new Date().toISOString().slice(0, 10), note: "" });
      if (asset.Status !== "using") setAssigneeSuggestions(await getAssigneeSuggestions());
      setAssignmentModal(true);
    } catch (error) {
      showError(error);
    }
  }

  async function submitAssignment(event) {
    event.preventDefault();
    try {
      const result = activeAsset.Status === "using"
        ? await returnAsset(activeAsset.Id, { returnedDate: assignmentForm.date, note: assignmentForm.note })
        : await assignAsset(activeAsset.Id, {
            userId: assignmentForm.userId ? Number(assignmentForm.userId) : null,
            assignedTo: assignmentForm.assignedTo,
            assignedDate: assignmentForm.date,
            note: assignmentForm.note
          });
      setAssignmentModal(false);
      await refreshAll(result.message);
    } catch (error) {
      showError(error);
    }
  }

  async function openHistory(asset) {
    try {
      setActiveAsset(asset);
      setHistoryData(await getAssetHistory(asset.Id));
      setHistoryModal(true);
    } catch (error) {
      showError(error);
    }
  }

  async function openQr(asset) {
    try {
      setActiveAsset(asset);
      setQrData(await getQr(asset.Id));
      setQrModal(true);
    } catch (error) { showError(error); }
  }

  async function openSoftware(asset) {
    try {
      setActiveAsset(asset);
      setSoftware(await getSoftware(asset.Id));
      setSoftwareForm({ softwareName: "", version: "", publisher: "", installedDate: "", notes: "" });
      setSoftwareModal(true);
    } catch (error) { showError(error); }
  }

  async function submitSoftware(event) {
    event.preventDefault();
    try {
      await addSoftware(activeAsset.Id, softwareForm);
      setSoftware(await getSoftware(activeAsset.Id));
      setSoftwareForm({ softwareName: "", version: "", publisher: "", installedDate: "", notes: "" });
    } catch (error) { showError(error); }
  }

  async function removeSoftware(id) {
    if (!window.confirm("Xóa phần mềm này?")) return;
    try {
      await deleteSoftware(activeAsset.Id, id);
      setSoftware(await getSoftware(activeAsset.Id));
    } catch (error) { showError(error); }
  }

  async function openImages(asset) {
    setActiveAsset(asset);
    setImageFeedback({ type: "", text: "" });
    setImageModal(true);
    try {
      setAssetImages(await getImages(asset.Id));
    } catch (error) {
      setImageFeedback({ type: "error", text: error?.response?.data?.message || "Không tải được danh sách hình ảnh." });
    }
  }

  async function submitImages(event) {
    // Sao chép FileList trước khi reset input; FileList có thể bị xóa theo input.
    const selected = Array.from(event.target.files || []);
    event.target.value = "";
    if (!selected.length) return;
    const invalidType = selected.find((file) => !["image/jpeg", "image/png", "image/webp"].includes(file.type));
    const tooLarge = selected.find((file) => file.size > 5 * 1024 * 1024);
    if (invalidType) return setImageFeedback({ type: "error", text: `${invalidType.name}: chỉ chấp nhận JPG, PNG hoặc WebP.` });
    if (tooLarge) return setImageFeedback({ type: "error", text: `${tooLarge.name}: dung lượng vượt quá 5 MB.` });
    if (assetImages.length + selected.length > 6) return setImageFeedback({ type: "error", text: `Tài sản chỉ được tối đa 6 ảnh. Hiện có ${assetImages.length} ảnh.` });
    try {
      setImageLoading(true);
      setImageFeedback({ type: "loading", text: `Đang tải ${selected.length} hình ảnh lên...` });
      const result = await uploadImages(activeAsset.Id, selected);
      setAssetImages(await getImages(activeAsset.Id));
      setImageFeedback({ type: "success", text: result.message });
    } catch (error) {
      const status = error?.response?.status;
      const fallback = status === 404
        ? "Không tìm thấy API upload. Hãy dừng và chạy lại backend V6.1."
        : status === 413 ? "Ảnh vượt quá dung lượng máy chủ cho phép." : "Tải hình ảnh thất bại.";
      setImageFeedback({ type: "error", text: error?.response?.data?.message || fallback });
    }
    finally { setImageLoading(false); }
  }

  async function makePrimaryImage(imageId) {
    try {
      const result = await setPrimaryImage(activeAsset.Id, imageId);
      setAssetImages(await getImages(activeAsset.Id));
      setImageFeedback({ type: "success", text: result.message });
    } catch (error) { setImageFeedback({ type: "error", text: error?.response?.data?.message || "Không thể đổi ảnh đại diện." }); }
  }

  async function removeAssetImage(imageId) {
    if (!window.confirm("Bạn có chắc chắn muốn xóa hình ảnh này?")) return;
    try {
      const result = await deleteAssetImage(activeAsset.Id, imageId);
      setAssetImages(await getImages(activeAsset.Id));
      setImageFeedback({ type: "success", text: result.message });
    } catch (error) { setImageFeedback({ type: "error", text: error?.response?.data?.message || "Không thể xóa hình ảnh." }); }
  }

  function handleAssetAction(action, asset) {
    if (!action) return;
    if (action === "edit") openEdit(asset);
    else if (action === "assignment") openAssignment(asset);
    else if (action === "history") openHistory(asset);
    else if (action === "qr") openQr(asset);
    else if (action === "software") openSoftware(asset);
    else if (action === "images") openImages(asset);
    else if (action === "delete") handleDelete(asset.Id);
  }

  function handleLogout() {
    logout();
    setCurrentUser(null);
  }

  if (!currentUser) return <Login onLogin={setCurrentUser} />;
  if (currentPage === "departments") {
    return <DepartmentManager currentUser={currentUser} onBack={() => { setCurrentPage("assets"); refreshAll(); }} />;
  }
  if (currentPage === "categories") {
    return <CategoryManager currentUser={currentUser} onBack={() => { setCurrentPage("assets"); refreshAll(); }} />;
  }
  if (currentPage === "users") {
    return <UserManager currentUser={currentUser} onBack={() => setCurrentPage("assets")} />;
  }

  const cards = [
    { id: "", code: "TẤT CẢ", name: "Toàn hệ thống", ...stats.overall },
    ...stats.departments.map((item) => ({ id: String(item.DepartmentId || ""), code: item.DepartmentCode, name: item.DepartmentName, ...item }))
  ];

  return (
    <main className="app-shell">
      <div className="dashboard-layout">
      <aside className="dashboard-sidebar">
      <header className="topbar">
        <div><p className="eyebrow">HEXA GROUP</p><h1>Quản lý tài sản</h1></div>
        <div className="account">
          <div><strong>{currentUser.fullName}</strong><span>{currentUser.role === "admin" ? "Quản trị viên" : "Nhân viên"}</span></div>
          <button className="danger-btn" onClick={handleLogout}>Đăng xuất</button>
        </div>
      </header>

      <section className="filter-panel">
        <div className="filters">
          <div className="search-box"><span>⌕</span><input value={filters.search} onChange={(e) => updateFilter("search", e.target.value)} placeholder="Tìm mã, tên, người dùng, IP, serial..." />{filters.search && <button onClick={() => updateFilter("search", "")}>×</button>}</div>
          <select value={filters.departmentId} onChange={(e) => updateFilter("departmentId", e.target.value)}>
            <option value="">Tất cả công ty</option>
            {departments.map((item) => <option key={item.Id} value={item.Id}>{item.DepartmentName}</option>)}
          </select>
          <select value={filters.status} onChange={(e) => updateFilter("status", e.target.value)}>
            <option value="">Tất cả trạng thái</option>
            {Object.entries(statusNames).map(([value, label]) => <option key={value} value={value}>{label}</option>)}
          </select>
          <select value={filters.categoryId} onChange={(e) => updateFilter("categoryId", e.target.value)}>
            <option value="">Tất cả loại</option>
            {categories.map((item) => <option key={item.Id} value={item.Id}>{item.CategoryName}</option>)}
          </select>
          <button className="reset-btn" onClick={resetFilters}>Đặt lại</button>
        </div>
        <div className="toolbar">
          <input ref={fileInputRef} className="hidden-input" type="file" accept=".xlsx" onChange={handleImport} />
          <select
            className="toolbar-menu"
            value=""
            disabled={excelLoading}
            aria-label="Quản lý và dữ liệu"
            onChange={(event) => {
              const action = event.target.value;
              event.target.value = "";
              handleManagementAction(action);
            }}
          >
            <option value="" disabled>{excelLoading ? "Đang xử lý Excel..." : "☰ Quản lý & dữ liệu"}</option>
            <optgroup label="Quản lý">
              <option value="departments">Công ty</option>
              <option value="categories">Loại tài sản</option>
              {currentUser.role === "admin" && <option value="users">Tài khoản</option>}
            </optgroup>
            <optgroup label="Excel">
              <option value="template">Tải file mẫu</option>
              {currentUser.role === "admin" && <option value="import">Import Excel</option>}
              <option value="export">Export Excel</option>
            </optgroup>
          </select>
          {currentUser.role === "admin" && <button className="primary-btn" onClick={openCreate}>+ Thêm tài sản</button>}
        </div>
      </section>

      {message && <div className="notice"><span>{message}</span><button onClick={() => { setMessage(""); setErrorDetails([]); }}>×</button></div>}
      {errorDetails.length > 0 && <div className="error-list"><strong>Chi tiết lỗi import:</strong>{errorDetails.map((item) => <div key={item.row || item.field}>{item.row ? `Dòng ${item.row}` : item.field}: {item.errors.join("; ")}</div>)}</div>}

      <section className="stats-grid">
        {cards.map((card, index) => (
          <button key={`${card.id}-${index}`} className={`stat-card ${filters.departmentId === card.id ? "active" : ""}`} onClick={() => updateFilter("departmentId", card.id)}>
            <span className="stat-code">{card.code}</span><span className="stat-name">{card.name}</span>
            <strong>{Number(card.Total || 0)}</strong>
            <div><span className="in-use">● {Number(card.InUse || 0)}</span><span className="other">▣ {Number(card.OtherCount || 0)}</span></div>
          </button>
        ))}
      </section>

      </aside>
      <section className="dashboard-content">

      <section className="table-card">
        <div className="table-title"><div><h2>Danh sách tài sản</h2><p>{pagination.total} kết quả</p></div><button onClick={() => refreshAll()}>↻ Làm mới</button></div>
        <div className="table-wrap">
          <table>
            <thead><tr>
              <th>Mã tài sản</th><th>Loại</th><th>Tên tài sản</th><th>Công ty</th>
              <th>Người sử dụng</th><th>Địa chỉ IP</th><th>Ghi chú</th><th>Trạng thái</th>
              {currentUser.role === "admin" && <th>Thao tác</th>}
            </tr></thead>
            <tbody>
              {loading ? <tr><td colSpan="9" className="empty-row">Đang tải dữ liệu...</td></tr> :
              assets.length === 0 ? <tr><td colSpan="9" className="empty-row">Không tìm thấy tài sản.</td></tr> :
              assets.map((asset) => <tr key={asset.Id}>
                <td className="asset-code">{asset.AssetCode}</td>
                <td><span className="type-badge">{asset.CategoryName || "Chưa phân loại"}</span></td>
                <td className="asset-name">{asset.AssetName}<small>{asset.SerialNumber || ""}</small></td>
                <td><span className="company-badge">{asset.DepartmentCode || asset.DepartmentName || "-"}</span></td>
                <td>{asset.AssignedTo || "—"}</td><td><span className="ip">{asset.IpAddress || "Chưa có IP"}</span></td>
                <td className="note-cell">{asset.Description || "—"}</td>
                <td><span className={`status ${asset.Status}`}>● {statusNames[asset.Status] || asset.Status}</span></td>
                {currentUser.role === "admin" && <td>
                  <select
                    className="action-select"
                    value=""
                    aria-label={`Thao tác với ${asset.AssetCode}`}
                    onChange={(event) => {
                      const action = event.target.value;
                      event.target.value = "";
                      handleAssetAction(action, asset);
                    }}
                  >
                    <option value="" disabled>Thao tác ▾</option>
                    <option value="edit">Sửa tài sản</option>
                    <option value="assignment">{asset.Status === "using" ? "Thu hồi" : "Bàn giao"}</option>
                    <option value="history">Xem lịch sử</option>
                    <option value="qr">Xem mã QR</option>
                    <option value="images">Hình ảnh tài sản</option>
                    {["LAPTOP", "PC", "DESKTOP"].includes(String(asset.CategoryCode || "").toUpperCase()) && <option value="software">Phần mềm đã cài</option>}
                    <option value="delete">Xóa tài sản</option>
                  </select>
                </td>}
              </tr>)}
            </tbody>
          </table>
        </div>
        <div className="pagination">
          <label>Hiển thị <select value={pagination.pageSize} onChange={(e) => setPagination((p) => ({ ...p, pageSize: Number(e.target.value), page: 1 }))}><option>10</option><option>20</option><option>50</option><option>100</option></select> dòng</label>
          <div><button disabled={pagination.page <= 1} onClick={() => setPagination((p) => ({ ...p, page: p.page - 1 }))}>‹ Trước</button><span>Trang {pagination.page} / {pagination.totalPages}</span><button disabled={pagination.page >= pagination.totalPages} onClick={() => setPagination((p) => ({ ...p, page: p.page + 1 }))}>Sau ›</button></div>
        </div>
      </section>

      </section>
      </div>

      {isAssetModalOpen && <div className="modal-overlay" onMouseDown={(e) => e.target === e.currentTarget && closeModal()}>
        <div className="asset-modal" role="dialog" aria-modal="true">
          <div className="modal-header"><div><p className="eyebrow">TÀI SẢN</p><h2>{editingId ? "Cập nhật tài sản" : "Thêm tài sản mới"}</h2></div><button className="modal-close" onClick={closeModal}>×</button></div>
          {formErrors.length > 0 && <div className="form-error-summary"><strong>Vui lòng kiểm tra:</strong><ul>{formErrors.map((item, index) => <li key={index}>{item}</li>)}</ul></div>}
          <form onSubmit={handleSubmit}>
            <div className="modal-form-grid">
              <label>Mã tài sản<input name="assetCode" value={form.assetCode} onChange={handleChange} maxLength="100" required autoFocus /></label>
              <label>Loại<select name="categoryId" value={form.categoryId} onChange={handleChange}><option value="">-- Chọn loại --</option>{categories.map((x) => <option key={x.Id} value={x.Id}>{x.CategoryName}</option>)}</select></label>
              <label className="wide">Tên tài sản<input name="assetName" value={form.assetName} onChange={handleChange} maxLength="255" required /></label>
              <label>Công ty<select name="departmentId" value={form.departmentId} onChange={handleChange}><option value="">-- Chọn công ty --</option>{departments.map((x) => <option key={x.Id} value={x.Id}>{x.DepartmentName}</option>)}</select></label>
              <label>Người sử dụng<input name="assignedTo" value={form.assignedTo} onChange={handleChange} maxLength="255" placeholder="Mã NV - Họ tên" /></label>
              <label>Địa chỉ IP<input name="ipAddress" value={form.ipAddress} onChange={handleChange} maxLength="15" placeholder="192.168.1.10" /></label>
              <label>Serial<input name="serialNumber" value={form.serialNumber} onChange={handleChange} maxLength="255" /></label>
              <label>Ngày mua<input type="date" name="purchaseDate" value={form.purchaseDate} onChange={handleChange} /></label>
              <label>Giá mua<input type="number" min="0" max="9999999999999999" step="0.01" name="purchasePrice" value={form.purchasePrice} onChange={handleChange} /></label>
              <label>Trạng thái<select name="status" value={form.status} onChange={handleChange}>{Object.entries(statusNames).map(([value, label]) => <option key={value} value={value}>{label}</option>)}</select></label>
              <label className="wide">Ghi chú<textarea name="description" value={form.description} onChange={handleChange} maxLength="1000" rows="3" /></label>
            </div>
            <div className="modal-footer"><button type="button" className="cancel-btn" onClick={closeModal}>Hủy</button><button type="submit" className="primary-btn">{editingId ? "Lưu thay đổi" : "Thêm tài sản"}</button></div>
          </form>
        </div>
      </div>}

      {assignmentModal && activeAsset && <div className="modal-overlay" onMouseDown={(e) => e.target === e.currentTarget && setAssignmentModal(false)}>
        <div className="asset-modal compact-modal">
          <div className="modal-header"><div><p className="eyebrow">{activeAsset.Status === "using" ? "THU HỒI" : "BÀN GIAO"}</p><h2>{activeAsset.AssetCode} – {activeAsset.AssetName}</h2></div><button className="modal-close" onClick={() => setAssignmentModal(false)}>×</button></div>
          <form onSubmit={submitAssignment}><div className="modal-form-grid">
            {activeAsset.Status !== "using" ? <label className="wide">Người nhận
              <input
                list="assignee-suggestions"
                value={assignmentForm.assignedTo}
                onChange={(e) => {
                  const value = e.target.value;
                  const matched = assigneeSuggestions.find((item) => item.DisplayName === value);
                  setAssignmentForm({ ...assignmentForm, assignedTo: value, userId: matched?.UserId ? String(matched.UserId) : "" });
                }}
                placeholder="Nhập tên hoặc chọn gợi ý..."
                autoComplete="off"
                required
              />
              <datalist id="assignee-suggestions">
                {assigneeSuggestions.map((item, index) => <option key={`${item.UserId || "past"}-${index}`} value={item.DisplayName}>{item.DepartmentCode ? `Công ty: ${item.DepartmentCode}` : "Tên đã sử dụng trước đây"}</option>)}
              </datalist>
              <small className="field-hint">Có thể nhập tên bất kỳ. Chọn gợi ý tài khoản để hệ thống tự liên kết người dùng và công ty.</small>
            </label> : <div className="assignment-current wide"><span>Đang được cấp cho</span><strong>{activeAsset.AssignedTo || "Không xác định"}</strong></div>}
            <label className="wide">{activeAsset.Status === "using" ? "Ngày thu hồi" : "Ngày bàn giao"}<input type="date" value={assignmentForm.date} onChange={(e) => setAssignmentForm({ ...assignmentForm, date: e.target.value })} required /></label>
            <label className="wide">Ghi chú<textarea rows="4" value={assignmentForm.note} onChange={(e) => setAssignmentForm({ ...assignmentForm, note: e.target.value })} /></label>
          </div><div className="modal-footer"><button type="button" className="cancel-btn" onClick={() => setAssignmentModal(false)}>Hủy</button><button className="primary-btn" type="submit">{activeAsset.Status === "using" ? "Xác nhận thu hồi" : "Xác nhận bàn giao"}</button></div></form>
        </div>
      </div>}

      {historyModal && activeAsset && <div className="modal-overlay" onMouseDown={(e) => e.target === e.currentTarget && setHistoryModal(false)}>
        <div className="asset-modal history-modal">
          <div className="modal-header"><div><p className="eyebrow">NHẬT KÝ TÀI SẢN</p><h2>{activeAsset.AssetCode} – {activeAsset.AssetName}</h2></div><button className="modal-close" onClick={() => setHistoryModal(false)}>×</button></div>
          <div className="history-layout">
            <section><h3>Dòng thời gian thay đổi</h3>{historyData.history.length === 0 ? <p className="muted-text">Chưa có lịch sử.</p> : <div className="timeline">{historyData.history.map((item) => <article key={item.Id}><span className={`history-dot ${item.ActionType.toLowerCase()}`}></span><div><strong>{item.Description}</strong><p>{new Date(item.CreatedAt).toLocaleString("vi-VN")} · {item.PerformedByName || "Hệ thống"}</p>{item.NewData && ["ASSIGN", "RETURN"].includes(item.ActionType) && <small>{item.NewData}</small>}</div></article>)}</div>}</section>
            <section><h3>Các lượt bàn giao</h3>{historyData.assignments.length === 0 ? <p className="muted-text">Chưa có lượt bàn giao.</p> : <div className="assignment-list">{historyData.assignments.map((item) => <article key={item.Id}><div><strong>{item.AssignedToSnapshot}</strong><span className={item.IsActive ? "active-assignment" : "returned-assignment"}>{item.IsActive ? "Đang sử dụng" : "Đã thu hồi"}</span></div><p>Bàn giao: {new Date(item.AssignedDate).toLocaleDateString("vi-VN")} bởi {item.AssignedByName || "—"}</p>{item.ReturnedDate && <p>Thu hồi: {new Date(item.ReturnedDate).toLocaleDateString("vi-VN")} bởi {item.ReturnedByName || "—"}</p>}{item.AssignNote && <small>Ghi chú giao: {item.AssignNote}</small>}{item.ReturnNote && <small>Ghi chú thu: {item.ReturnNote}</small>}</article>)}</div>}</section>
          </div>
        </div>
      </div>}

      {qrModal && activeAsset && qrData && <div className="modal-overlay" onMouseDown={(e) => e.target === e.currentTarget && setQrModal(false)}>
        <div className="asset-modal qr-modal"><div className="modal-header"><div><p className="eyebrow">MÃ QR TÀI SẢN</p><h2>{activeAsset.AssetCode}</h2></div><button className="modal-close" onClick={() => setQrModal(false)}>×</button></div>
          <img src={qrData.qrDataUrl} alt={`QR ${activeAsset.AssetCode}`} /><p>Quét để xem thông tin và lịch sử tài sản</p>
          <div className="qr-buttons"><a href={qrData.qrDataUrl} download={`QR-${activeAsset.AssetCode}.png`}>Tải ảnh QR</a><a href={qrData.publicUrl} target="_blank" rel="noreferrer">Mở trang thông tin</a></div>
        </div>
      </div>}

      {softwareModal && activeAsset && <div className="modal-overlay" onMouseDown={(e) => e.target === e.currentTarget && setSoftwareModal(false)}>
        <div className="asset-modal software-modal"><div className="modal-header"><div><p className="eyebrow">PHẦN MỀM ĐÃ CÀI</p><h2>{activeAsset.AssetCode} – {activeAsset.AssetName}</h2></div><button className="modal-close" onClick={() => setSoftwareModal(false)}>×</button></div>
          <form onSubmit={submitSoftware}><div className="software-form"><input placeholder="Tên phần mềm *" value={softwareForm.softwareName} onChange={(e) => setSoftwareForm({ ...softwareForm, softwareName: e.target.value })} required /><input placeholder="Phiên bản" value={softwareForm.version} onChange={(e) => setSoftwareForm({ ...softwareForm, version: e.target.value })} /><input placeholder="Nhà phát hành" value={softwareForm.publisher} onChange={(e) => setSoftwareForm({ ...softwareForm, publisher: e.target.value })} /><input type="date" value={softwareForm.installedDate} onChange={(e) => setSoftwareForm({ ...softwareForm, installedDate: e.target.value })} /><button className="primary-btn">+ Thêm</button></div></form>
          <div className="software-list">{software.length === 0 ? <p>Chưa có phần mềm.</p> : software.map((item) => <article key={item.Id}><div><strong>{item.SoftwareName}</strong><span>{item.Version || "Không rõ phiên bản"}{item.Publisher ? ` · ${item.Publisher}` : ""}</span></div><button onClick={() => removeSoftware(item.Id)}>Xóa</button></article>)}</div>
        </div>
      </div>}

      {imageModal && activeAsset && <div className="modal-overlay" onMouseDown={(e) => e.target === e.currentTarget && setImageModal(false)}>
        <div className="asset-modal image-modal">
          <div className="modal-header"><div><p className="eyebrow">HÌNH ẢNH TÀI SẢN</p><h2>{activeAsset.AssetCode} – {activeAsset.AssetName}</h2></div><button className="modal-close" onClick={() => setImageModal(false)}>×</button></div>
          <div className="image-toolbar">
            <div><strong>{assetImages.length}/6 ảnh</strong><span>JPG, PNG hoặc WebP · tối đa 5 MB/ảnh</span></div>
            <input ref={imageInputRef} className="hidden-input" type="file" accept="image/jpeg,image/png,image/webp" multiple onChange={submitImages} />
            <button className="primary-btn" disabled={imageLoading || assetImages.length >= 6} onClick={() => imageInputRef.current?.click()}>{imageLoading ? "Đang tải..." : "+ Tải hình ảnh"}</button>
          </div>
          {imageFeedback.text && <div className={`image-feedback ${imageFeedback.type}`}>{imageFeedback.text}</div>}
          {assetImages.length === 0 ? <div className="image-empty">Tài sản chưa có hình ảnh.</div> : <div className="asset-image-grid">
            {assetImages.map((image) => <article key={image.Id} className={image.IsPrimary ? "primary-image" : ""}>
              <img src={image.Url} alt={image.FileName} />
              <div><span title={image.FileName}>{image.FileName}</span><small>{(image.FileSize / 1024 / 1024).toFixed(2)} MB</small></div>
              {image.IsPrimary ? <strong>Ảnh đại diện</strong> : <button onClick={() => makePrimaryImage(image.Id)}>Đặt đại diện</button>}
              <button className="image-delete" onClick={() => removeAssetImage(image.Id)}>Xóa</button>
            </article>)}
          </div>}
        </div>
      </div>}
    </main>
  );
}

export default App;
