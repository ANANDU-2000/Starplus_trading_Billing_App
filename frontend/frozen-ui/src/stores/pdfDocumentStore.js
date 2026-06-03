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
  /** UI hint only — never auto-triggers print/save (caused blank page print dialog) */
  mode: 'view',

  openPdfDocument: ({
    title = 'Document',
    filename = 'document.pdf',
    fetchPdf,
    mode = 'view'
  }) => {
    if (!fetchPdf) return
    set({
      isOpen: true,
      title,
      filename,
      fetchPdf,
      mode
    })
  },

  closePdfDocument: () => set({
    isOpen: false,
    fetchPdf: null,
    mode: 'view'
  })
}))
