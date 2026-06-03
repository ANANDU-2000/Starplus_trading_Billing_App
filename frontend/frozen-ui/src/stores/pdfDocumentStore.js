import { create } from 'zustand'

/**
 * Global PDF viewer modal — opened from invoicePdfActions and pages.
 * @typedef {'view'|'download'|'print'} PdfDocumentMode
 */

export const usePdfDocumentStore = create((set) => ({
  isOpen: false,
  title: 'Document',
  filename: 'document.pdf',
  fetchPdf: null,
  mode: 'view',
  autoPrint: false,
  autoSave: false,

  openPdfDocument: ({
    title = 'Document',
    filename = 'document.pdf',
    fetchPdf,
    mode = 'view',
    autoPrint = false,
    autoSave = false
  }) => {
    if (!fetchPdf) return
    set({
      isOpen: true,
      title,
      filename,
      fetchPdf,
      mode,
      autoPrint: mode === 'print' || autoPrint,
      autoSave: mode === 'download' || autoSave
    })
  },

  closePdfDocument: () => set({
    isOpen: false,
    fetchPdf: null,
    autoPrint: false,
    autoSave: false
  })
}))
