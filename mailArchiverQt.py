# -*- coding: utf-8 -*-
#-------------------------------------------------------------------------------
# Name:        MailArchiver
# Purpose:     Mail Archiving Tool for Windows and Linux, based on Qt and Python
#
# Author:      MEYER
#
# Created:     23.02.2017
# Copyright:   (c) Meyer 2017
# Licence:     GNU General Public License v3.0
#-------------------------------------------------------------------------------

import sys
import os
import os.path
import sys
import re
import subprocess
import imaplib
import dateutil.parser
import hashlib
import json
import email, email.header
import shutil
import time

try:
    from PyQt5.QtWidgets import QMainWindow, QMessageBox, QAbstractItemView, QApplication, QTableView, QWidget, QListView, QFileDialog
    from PyQt5.QtGui import QStandardItemModel, QStandardItem, QRegExpValidator
    from PyQt5.QtCore import QObject, pyqtSignal, QSortFilterProxyModel, QModelIndex, QTimer, pyqtSlot, QRegExp, QCoreApplication
    from PyQt5.QtSql import QSqlQuery, QSqlDatabase, QSqlQueryModel 
    from PyQt5 import uic, QtCore
except:
    from PyQt4.QtGui import QMainWindow, QMessageBox, QAbstractItemView, QApplication, QTableView, QWidget, QStandardItemModel, QStandardItem, QListView, QFileDialog, QSortFilterProxyModel, QRegExpValidator
    from PyQt4.QtSql import QSqlQuery, QSqlDatabase, QSqlQueryModel
    from PyQt4.QtCore import QModelIndex, QTimer, QObject, pyqtSignal, QRegExp, QCoreApplication
    from PyQt4 import uic, QtCore

try:
    from configparser import ConfigParser
except ImportError:
    from ConfigParser import ConfigParser  # ver. < 3.0
    
def getUniqueList(list):
    seen = set()
    uniq = []
    for x in list:
        if x not in seen:
            uniq.append(x.strip())
            seen.add(x)    
    return uniq

class TypingDelayDetector(QObject):

    finished = pyqtSignal()

    def run(self):
        timestamp = time.time()
        time.sleep(0.2)
        print("thread")
        self.finished.emit()

class CustomSortFilterProxyModel(QSortFilterProxyModel):
    def __init__(self, parent=None):
        super(CustomSortFilterProxyModel, self).__init__(parent)
    
    def setFilterTextBoxDict(self, filterTextBoxDict):
        self.filterTextBoxDict = filterTextBoxDict
        self.invalidateFilter()
 
    def filterAcceptsRow(self, row_num, parent):
        model = self.sourceModel()
        
        filterAll = []
        filterSpecific = []
        
        def filterField(filterString, field):
            if filterString.strip() == "":
                return True
            return not re.match( ".*" + filterString.lower(), field.lower()) == None
            
        filterStringAll = self.filterTextBoxDict["all"].text()
        
        filterString = self.filterTextBoxDict["subject"].text()
        fieldString = str(model.data(model.index(row_num,2)))
        filterAll.append(filterField(filterStringAll, fieldString))
        filterSpecific.append(filterField(filterString, fieldString))
        
        filterString = self.filterTextBoxDict["from"].text()
        fieldString = str(model.data(model.index(row_num,3)))
        filterAll.append(filterField(filterStringAll, fieldString))
        filterSpecific.append(filterField(filterString, fieldString))
        
        recipient_tmp = []
        filterString = self.filterTextBoxDict["to"].text()
        fieldString = str(model.data(model.index(row_num,4)))
        filterAll.append(filterField(filterStringAll, fieldString))
        recipient_tmp.append(filterField(filterString, fieldString))
        
        if self.filterTextBoxDict["cc"].isChecked():
            filterString = self.filterTextBoxDict["to"].text()
            fieldString = str(model.data(model.index(row_num,5)))
            filterAll.append(filterField(filterStringAll, fieldString))
            recipient_tmp.append(filterField(filterString, fieldString))
                        
        if self.filterTextBoxDict["bcc"].isChecked():
            filterString = self.filterTextBoxDict["to"].text()
            fieldString = str(model.data(model.index(row_num,6)))
            filterAll.append(filterField(filterStringAll, fieldString))
            recipient_tmp.append(filterField(filterString, fieldString))
        
        filterSpecific.append(True in recipient_tmp)
            
        filterString = self.filterTextBoxDict["receive_time"].text()
        fieldString = str(model.data(model.index(row_num,7)))
        filterAll.append(filterField(filterStringAll, fieldString))
        filterSpecific.append(filterField(filterString, fieldString))
        
        fieldString = str(model.data(model.index(row_num,8)))
        filterString = self.filterTextBoxDict["attachments_name"].text()
        filterAll.append(filterField(filterStringAll, fieldString))
        filterSpecific.append(filterField(filterString, fieldString))
        if self.filterTextBoxDict["attachments_yes"].isChecked() != self.filterTextBoxDict["attachments_no"].isChecked():  
            if self.filterTextBoxDict["attachments_yes"].isChecked():  
                if fieldString == "None":
                    filterSpecific.append(False)                
            else:
                fieldString = str(model.data(model.index(row_num,8)))
                filterSpecific.append(fieldString == "None")
                
        filterString = self.filterTextBoxDict["account"].currentText()
        fieldString = str(model.data(model.index(row_num,9)))
        filterAll.append(filterField(filterStringAll, fieldString))
        if filterString != "All":
            filterSpecific.append(filterField(filterString, fieldString))
   
        filterString = self.filterTextBoxDict["folder"].currentText()
        fieldString = str(model.data(model.index(row_num,10)))
        filterAll.append(filterField(filterStringAll, fieldString))
        if filterString != "All":
            filterSpecific.append(filterField(filterString, fieldString))
            
        filterString = self.filterTextBoxDict["message"].text()
        fieldString = str(model.data(model.index(row_num,11)))
        filterAll.append(filterField(filterStringAll, fieldString))
        filterSpecific.append(filterField(filterString, fieldString))
               
        if not True in filterAll:
            return False
        if False in filterSpecific:
            return False
        return True
            
class pyMailArchiver(QMainWindow):
    
    def __init__(self,parent=None):
        # Set up the user interface from Designer.
        super(pyMailArchiver, self).__init__()
        uic.loadUi("pyMailArchiverInterface.ui",self)
        self.show()
                        
        self.read_config()
        
        
        self.m_typingTimer =  QTimer()
        self.m_typingTimer.setSingleShot( True )

        self.m_typingTimer.timeout.connect(self.filterEntries)
        self.line_filter_all.textChanged.connect(self.onTextEdited)
        self.line_filter_subject.textChanged.connect(self.onTextEdited)
        self.line_filter_from.textChanged.connect(self.onTextEdited)
        self.line_filter_to.textChanged.connect(self.onTextEdited)
        self.line_filter_timestamp.textChanged.connect(self.onTextEdited)
        self.line_filter_attachments.textChanged.connect(self.onTextEdited)
        self.line_filter_message.textChanged.connect(self.onTextEdited)
        
        
        
        int_validator = QRegExpValidator(QRegExp("^[0-9]*$"))
        self.line_add_port.setValidator(int_validator)
        self.pBar_progress_import.setMinimum(0)
        self.pBar_progress_import.setValue(0)
        
        self.lastFilterCall = time.time()
        
        self.threadDump = []
        
        ##### Database Setup with QtSQL
        db_exist = os.path.isfile(self.db_path)
        
        self.database = QSqlDatabase.addDatabase("QSQLITE")

        self.database.setDatabaseName(self.db_path)
        self.database.open()   
        self.dbquery = QSqlQuery()
                        
        if db_exist == False:
            # Create new database if none is there
            self.dbquery.prepare('''CREATE TABLE `mails` (`MailID`	INTEGER PRIMARY KEY AUTOINCREMENT UNIQUE,	`MailHash`	TEXT, `Subject` TEXT, `Sender` TEXT, `Recipient` TEXT, `CC_recp` TEXT, `BCC_recp` TEXT, `receive_time` TIMESTAMP, `attachments` TEXT, `account` TEXT, `folder` TEXT, `Message` TEXT)''')
            self.dbquery.exec_()            
            self.database.commit()
            
        # Setup TableModel to populate tables in UI directly from database
        self.model_mails = QSqlQueryModel(self)
        #self.model_mails.setTable("mails")
        self.model_mails.setQuery("SELECT * from mails ORDER BY receive_time DESC")
        #self.model_mails.select()
        
        class dummy:
            def text(self):
                return ""
        self.proxy = CustomSortFilterProxyModel()
        self.proxy.setFilterTextBoxDict({"all": self.line_filter_all, "subject": self.line_filter_subject, "from": self.line_filter_from, "to": self.line_filter_to, "cc": self.cBox_filter_cc, "bcc": self.cBox_filter_bcc, "receive_time": self.line_filter_timestamp, "attachments_yes": self.cBox_filter_attachment_yes, "attachments_no": self.cBox_filter_attachment_no, "attachments_name": self.line_filter_attachments, "account": self.cBox_filter_account, "folder": self.cBox_filter_folder, "message": self.line_filter_message})
        self.proxy.setSourceModel(self.model_mails)
        
        #self.tableView_mails.setModel(self.proxy)
        self.tableView_mails.setModel(self.model_mails)
        self.tableView_mails.setColumnHidden(0,True)
        self.tableView_mails.setColumnHidden(1,True)
        self.tableView_mails.setColumnHidden(5,True)
        self.tableView_mails.setColumnHidden(6,True)
        self.tableView_mails.setColumnHidden(8,True)
        self.tableView_mails.setColumnHidden(9,True)
        self.tableView_mails.setColumnHidden(10,True)
        self.tableView_mails.setColumnHidden(11,True)
        self.tableView_mails.setEditTriggers(QAbstractItemView.NoEditTriggers)
        self.tableView_mails.verticalHeader().setVisible(False)
        self.tableView_mails.setSortingEnabled(True)
        #self.tableView_mails.sortByColumn(7,QtCore.Qt.DescendingOrder)
        self.tableView_mails.update()
                    
        # populate Account Name combobox
        self.cBox_account.addItem("-- Account --")
        for filename in os.listdir("."):
            if filename.endswith(".account"):
                self.cBox_account.addItem(filename.rsplit(".",1)[0])
                
        self.dbquery.prepare("SELECT DISTINCT account FROM mails")
        self.dbquery.exec_()        
        while self.dbquery.next():
            self.cBox_filter_account.addItem(self.dbquery.value(0))
        
        self.dbquery.prepare("SELECT DISTINCT folder FROM mails")
        self.dbquery.exec_()        
        while self.dbquery.next():
            self.cBox_filter_folder.addItem(self.dbquery.value(0))
        
                               
        return
        
    def read_config(self,filename="pyMailArchiverConfig.ini"):
        config = ConfigParser()
        config.read(filename)
        
        #### Global
        try:
            self.db_path = config["Global"]["DatabaseFile"]
            print(self.db_path)
        except:
            QMessageBox.warning(self, 'Invalid config value', 'DatabaseFile value for Global in config file invalid', QMessageBox.Ok)
        
        try:
            self.archiveBasePath = config["Global"]["archiveBasePath"]
            self.archiveBasePath = self.archiveBasePath.replace("\\\\","/").replace("\\","/")
            if self.archiveBasePath[-1] != "/":
                self.archiveBasePath += "/"
            print(self.archiveBasePath)
        except:
            QMessageBox.warning(self, 'Invalid config value', 'archiveBasePath value for Global in config file invalid', QMessageBox.Ok)
         
    def filterEntries(self):
        queryString = "SELECT * FROM mails"
        
        # variables for query. store them in list b/c QSqlQuery.addBindValue can only be called after QSqlQuery.prepare
        bindValueList = []

        # build query string based on actually used filter fields.
        filterString = self.line_filter_subject.text()
        if filterString != "":
            queryString += " WHERE (Subject LIKE ?"
            bindValueList.append("%" + filterString + "%")
        
        filterString = self.line_filter_message.text()
        if filterString != "":
            if len(bindValueList) > 0:
                queryString += " AND "
            else:
                queryString += " WHERE ("
            queryString += "Message LIKE ?"
            bindValueList.append("%" + filterString + "%")
            
        filterString = self.line_filter_timestamp.text()
        if filterString != "":
            if len(bindValueList) > 0:
                queryString += " AND "
            else:
                queryString += " WHERE ("
            queryString += "receive_time LIKE ?"
            bindValueList.append("%" + filterString + "%")
        
        filterString = self.line_filter_from.text()
        if filterString != "":
            if len(bindValueList) > 0:
                queryString += " AND "
            else:
                queryString += " WHERE ("
            queryString += "Sender LIKE ?"
            bindValueList.append("%" + filterString + "%")
            
        filterString = self.line_filter_to.text()
        if filterString != "":
            if len(bindValueList) > 0:
                queryString += " AND "
            else:
                queryString += " WHERE ("
            if self.cBox_filter_cc.isChecked() or self.cBox_filter_bcc.isChecked():
                queryString += "(Recipient LIKE ?"
                bindValueList.append("%" + filterString + "%")
                if self.cBox_filter_cc.isChecked():
                    queryString += " OR CC_recp LIKE ?"
                    bindValueList.append("%" + filterString + "%")
                if self.cBox_filter_bcc.isChecked():
                    queryString += " OR BCC_recp LIKE ?"
                    bindValueList.append("%" + filterString + "%")
                queryString += ")"
            else:
                queryString += "Recipient LIKE ?"
                bindValueList.append("%" + filterString + "%")
        
        filterString = self.line_filter_attachments.text()        
        if filterString != "":
            if len(bindValueList) > 0:
                queryString += " AND "
            else:
                queryString += " WHERE ("
            queryString += "attachments LIKE ?"
            bindValueList.append("%" + filterString + "%")
        elif self.cBox_filter_attachment_yes.isChecked() != self.cBox_filter_attachment_no.isChecked():
            if len(bindValueList) > 0:
                queryString += " AND "
            else:
                queryString += " WHERE ("
            if self.cBox_filter_attachment_yes.isChecked():
                queryString += "attachments != ?"
            else:
                queryString += "attachments = ?"
            bindValueList.append("None")
            
        if self.cBox_filter_account.currentIndex() != 0:
            if len(bindValueList) > 0:
                queryString += " AND "
            else:
                queryString += " WHERE ("
            queryString += "account = ?"
            bindValueList.append(self.cBox_filter_account.currentText())
        
        if self.cBox_filter_folder.currentIndex() != 0:    
            if len(bindValueList) > 0:
                queryString += " AND "
            else:
                queryString += " WHERE ("
            queryString += "folder = ?"
            bindValueList.append(self.cBox_filter_folder.currentText())
            
        if len(bindValueList) > 0:
            queryString += ")"
            
        # add filtering of all fields if filter line "all" is used
        filterString = self.line_filter_all.text()
        if filterString != "":
            if len(bindValueList) > 0:
                queryString += " AND "
            else:
                queryString += " WHERE "
                
            queryString += "(Subject LIKE ? OR Message LIKE ? OR receive_time LIKE ? OR Sender LIKE ? OR Recipient LIKE ? OR CC_recp LIKE ? OR BCC_recp LIKE ? OR attachments LIKE ? OR account LIKE ? OR folder LIKE ?)"
            
            for i in range(10):
                bindValueList.append("%" + filterString + "%")
                
        queryString +=  " ORDER BY receive_time DESC"
        
        # prepare query and add bind values
        self.dbquery.prepare(queryString)
        for value in bindValueList:
            self.dbquery.addBindValue(value)
            
        self.dbquery.exec_()
        
        self.model_mails.setQuery(self.dbquery)
        print("filtering")
        
    def onTextEdited(self):
        self.m_typingTimer.start(200)

        
    @pyqtSlot(int)
    def on_cBox_filter_cc_stateChanged(self,state):
        print(5)
        self.filterEntries()
        
    @pyqtSlot(int)
    def on_cBox_filter_bcc_stateChanged(self,state):
        print(6)
        self.filterEntries()
        
    @pyqtSlot(int)
    def on_cBox_filter_attachment_yes_stateChanged(self,state):
        print(8)
        self.filterEntries()
        
    @pyqtSlot(int)
    def on_cBox_filter_attachment_no_stateChanged(self,state):
        print(9)
        self.filterEntries()
        
    @pyqtSlot(int)
    def on_cBox_filter_account_currentIndexChanged(self,state):
        print(11)
        self.filterEntries()        
        
    @pyqtSlot(int)
    def on_cBox_filter_folder_currentIndexChanged(self,state):
        print(12)
        self.filterEntries()   
               
    @pyqtSlot(int)
    def on_cBox_password_dont_save_stateChanged(self, state):
        self.line_add_password.setEnabled(not state)
        
    @pyqtSlot(QModelIndex)
    def on_tableView_mails_doubleClicked(self, index):
        row = index.row()
        
        mailID = self.tableView_mails.model().data(self.tableView_mails.model().index(row,0))
        filepath = self.constructFilePath(mailID)
        
        print("opening ", filepath)
        #os.startfile(filepath)
        if sys.platform == "win32":
            os.startfile(filepath)
        else:
            opener ="open" if sys.platform == "darwin" else "xdg-open"
            subprocess.call(["thunderbird", filepath])
        
        
    @pyqtSlot()
    def on_btn_add_account_clicked(self):
        server = self.line_add_server.text()
        port = self.line_add_port.text()
        username = self.line_add_username.text()
        if self.cBox_password_dont_save.isChecked():
            password = "input"
        else:
            password = self.line_add_password.text()
            
        if "" in [server,port,username,password]:
            QMessageBox.warning(self, 'Incomplete form!', 'Please fill all fields before continuing.', QMessageBox.Ok)
            return
            
        if self.cBox_password_dont_save.isChecked():
            text, ok = QtGui.QInputDialog.getText(self, 'Input Dialog', 'Enter password:', mode=QtGui.QLineEdit.Password)
            if ok:
                password = text
            else:
                return
            
        try:
            server = imaplib.IMAP4_SSL(server, int(port))
            server.login(username, password)
        except:
            QMessageBox.warning(self, 'Connection failed!', 'Connecting to server failed. Check server settings and credentials.', QMessageBox.Ok)
            return
            
        accountConfig = ConfigParser()
        accountConfig["server"] = {"name": server, "port": port}
        accountConfig["credentials"] = {"name": username, "pass": password}
        with open(username + ".account", "w") as configfile:
            accountConfig.write(configfile)
        
        QMessageBox.information(self, 'Connection succesfull!', 'Successfully connected to server! Settings saved!', QMessageBox.Ok)
        return
        
        
    
    @pyqtSlot()
    def on_btn_archive_new_clicked(self):
        account = self.cBox_account.currentText()
        if account == "-- Select --":
            return
        
        # read config data from account file
        accountData = ConfigParser()
        accountData.read(account + ".account")
                
        serverName = accountData['server']['name']
        serverPort = accountData['server']['port']
        username = accountData['credentials']['name']
        password = accountData['credentials']['pass']
        exclude_patterns = accountData['settings']['excludedFolders']
        
        # process config data
        if username.lower() == "input":
            text, ok = QtGui.QInputDialog.getText(self, 'Input Dialog', 
            'Enter username:')
            if ok:
                username = text
            else:
                return
                
        if password.lower() == "input":
            text, ok = QtGui.QInputDialog.getText(self, 'Input Dialog', 'Enter password:', mode=QtGui.QLineEdit.Password)
            if ok:
                password = text
            else:
                return
        
        try:
            exclude_patterns = json.loads(exclude_patterns)
            if len(exclude_patterns) > 0:
                exclude_patterns_compiled = [re.compile(pattern) for pattern in exclude_patterns]
            else:
                exclude_patterns_compiled = False
        except:
            QMessageBox.warning(self, 'Parsing excluded folders failed!', 'Parsing excluded folders failed! Check config file and make sure excludedFolders is a list of valid regex expressions.', QMessageBox.Ok)
            return
            
        # connect to server
        try:
            server = imaplib.IMAP4_SSL(serverName, int(serverPort))
            server.login(username, password)
        except:
            QMessageBox.warning(self, 'Connection failed!', 'Connecting to server failed. Check server settings and credentials in config file.', QMessageBox.Ok)
            return
                    
        # Get list of all folders
        typ,data = server.list()
        serverFolders = []
        localFolders = []
        for folder in data:
            folder_str = folder.decode("utf-8")
            print(folder)
            print(folder_str)
            folder_str = folder_str.split('"/"',1)[1].strip()
            serverFolders.append(folder_str)
            folder_str = folder_str.replace('"','') + "/"
            localFolders.append(folder_str)
        
        new_mails = 0
        for i in range(len(serverFolders)):
            serverFolder = serverFolders[i]
            localFolder = localFolders[i]
            self.label_progress_folder.setText("Processing Folder: " + localFolder)
            print("----")
            # skip folder if in exclude list
            skip = False
            if exclude_patterns_compiled:
                for pattern in exclude_patterns_compiled:   
                    if pattern.match(localFolder):
                        print("skipping", localFolder)
                        skip = True
                        break
            if skip:
                print("now skipping")
                continue
            
            print(serverFolder)
            server.select(serverFolder)
            typ, data = server.search(None, 'ALL')
            
            # iterate over mails in folder
            num_emails = len(data[0].split())
            self.pBar_progress_import.setMinimum(0)
            self.pBar_progress_import.setMaximum(num_emails)
            mailCount = 0
            for num in data[0].split():
                mailCount += 1
                self.label_progress_email.setText("E-Mail: %s/%s" %(mailCount, num_emails))
                self.pBar_progress_import.setValue(mailCount)
                QCoreApplication.processEvents()
                header = {}
                print("------")
                print(localFolder)
                # get basic data to create hash
                typ, data = server.fetch(num, '(BODY[HEADER.FIELDS (date)])')
                header["date"] = data[0][1].decode("utf-8").split(":",1)[1].split("\r")[0][1:].strip()
               
                typ, data = server.fetch(num, '(BODY[HEADER.FIELDS (subject)])')
                header["subject"] = data[0][1].decode("utf-8").strip()
                
                typ, data = server.fetch(num, '(BODY[HEADER.FIELDS (from)])')
                header["from"] = data[0][1].decode("utf-8").strip()
                
                typ, data = server.fetch(num, '(BODY[HEADER.FIELDS (to)])')
                header["to"] = data[0][1].decode("utf-8").strip()
                                
                typ, data = server.fetch(num, '(BODY[HEADER.FIELDS (cc)])')
                header["cc"] = data[0][1].decode("utf-8").strip()
                                
                typ, data = server.fetch(num, '(BODY[HEADER.FIELDS (bcc)])')
                header["bcc"] = data[0][1].decode("utf-8").strip()
                
                header = self.parseHeader(header)
                
                unique_hash = self.createMailHash(header)
                print("header", header)
                print("hash", unique_hash)
                if self.checkMailExist(unique_hash):
                    # message already exists
                    print("SKIPPING MAIL SKIPPING MAIL SKIPPING MAIL SKIPPING MAIL SKIPPING MAIL SKIPPING MAIL SKIPPING MAIL SKIPPING MAIL")
                    continue
                new_mails += 1
                #print("archiving")
                typ, data = server.fetch(num, '(RFC822)')
                message_bytes = data[0][1]
                message = email.message_from_bytes(message_bytes)
                
                self.saveMailToDatabase(message, account, localFolder)
               
                
                # get MailID from database
                self.dbquery.prepare("SELECT MailID FROM mails WHERE MailHash=?")
                self.dbquery.addBindValue(unique_hash)
                self.dbquery.exec_()
                self.dbquery.next()
                MailID = self.dbquery.value(0)
                
                # generate savepath and save mail to file
                savename = self.constructFilePath(MailID)
                savepath = savename.rsplit("/",1)[0]
                if not os.path.exists(savepath):
                    os.makedirs(savepath)
                f = open(savename, 'wb')
                f.write(message_bytes)
                f.close()
                
                
        self.tableView_mails.update()
        print("new mails", new_mails)
        self.label_progress_folder.setText("Finished succesfully!")
        self.label_progress_email.setText("%s new Mails" % new_mails)
        
    def parseHeader(self, header):
        ret_header = {}
        print(header)
        print(header["date"])
        timeString = header["date"]
        try:
            timestamp = dateutil.parser.parse(header["date"].replace("(","").replace(")",""))
        except(ValueError):
                timestamp = dateutil.parser.parse(header["date"].replace("(","").replace(")","").rsplit(" ", 1)[0])
            
        
        
        """
        # catch variations of empty subjects
            if header["subject"] in ["\r\n", "\n", "\r"] or header["subject"] == None:
                subject = ""
            else:
                subjectList = email.header.decode_header(header["subject"])
                subject = ""
                for subjectPart in subjectList:
                    if type(subjectPart[0]) == bytes:
                        print(subjectPart)
                        print(type(subjectPart))
                        print(type(subjectPart[0]))
                        print(type(subjectPart[1]))
                        if subjectPart[1] == None:
                            subject += subjectPart[0].decode(errors="replace")
                        else:
                            subject += subjectPart[0].decode(subjectPart[1],errors="replace")
                    else:
                        try:
                            subject += str(subjectPart[0])
                        except:
                            pass
                # someteimes there is a "Subject:" in front of the subject
                # get rid of this
                if len(subject) > 7:
                    if subject[0:8] == "Subject:":
                        if len(subject) > 8:
                            subject = subject[8:]
                        else:
                            subject = ""
        """                    
                            
        
        def decode_header_field(fieldString):
            # catch variations of empty subjects
            if fieldString in ["\r\n", "\n", "\r"] or fieldString == None:
                returnString = ""
            else:
                fieldList = email.header.decode_header(fieldString)
                returnString = ""
                for fieldPart in fieldList:
                    if type(fieldPart[0]) == bytes:
                        print(fieldPart)
                        print(type(fieldPart))
                        print(type(fieldPart[0]))
                        print(type(fieldPart[1]))
                        if fieldPart[1] == None:
                            returnString += fieldPart[0].decode(errors="replace")
                        else:
                            returnString += fieldPart[0].decode(fieldPart[1],errors="replace")
                    else:
                        try:
                            returnString += str(fieldPart[0])
                        except:
                            pass
            
            return returnString
            
        
        subject = decode_header_field(header["subject"]).replace("Subject:", "").strip()
        sender = decode_header_field(header["from"]).replace("From:", "").strip()
        recipient = decode_header_field(header["to"]).replace("To:", "").strip()
        cc = decode_header_field(header["cc"]).replace("CC:", "").strip()
        bcc = decode_header_field(header["bcc"]).replace("BCC:", "").strip()
        
       
    
        def parseMailAdressList(AdressList):
            adList = re.findall(r'[\w\.-]+@[\w\.-]+', AdressList)
            adList = getUniqueList(adList)
            return adList
            
        ret_header["subject"]            = subject
        ret_header["date"]               = timestamp
        
        ret_header["from"]               = sender
        ret_header["to"]                 = recipient
        ret_header["cc"]                 = cc
        ret_header["bcc"]                = bcc
        
        ret_header["from_only_email"]    = parseMailAdressList(header["from"])
        ret_header["to_only_email"]      = parseMailAdressList(header["to"])
        ret_header["cc_only_email"]      = parseMailAdressList(header["cc"])
        ret_header["bcc_only_email"]     = parseMailAdressList(header["bcc"])
        
        return ret_header
            
    def getMessageHeader(self,message):
        header = {}
        sender = message["from"]
        if sender == None:
            sender = ""
        header["from"] = sender
               
        recipient = message["to"]
        if recipient == None:
            recipient = ""
        header["to"] = recipient
        
        cc = message["cc"]
        if cc == None:
            cc = ""
        header["cc"] = cc
        
        bcc = message["bcc"]
        if bcc == None:
            bcc = ""
        header["bcc"] = bcc
        
        header["date"] = message["date"]
        header["subject"] = message["subject"]
        header = self.parseHeader(header)
        
        return header
                
    def saveMailToDatabase(self, message, account, localFolder):
        if localFolder.endswith("/"):
            localFolder = localFolder[:-1]
        if localFolder.startswith("/"):
            localFolder = localFolder[1:]
    
        header = self.getMessageHeader(message)
        messageBody, attachments = self.parseMessage(message)
        
        
        def jsondump_list(dumplist):
            if len(dumplist) > 1:
                dumplist = json.dumps(dumplist)
            elif type(dumplist) == type([1]) and len(dumplist) == 0:
                dumplist = "None"
            elif type(dumplist) == type([1]):
                dumplist = dumplist[0]
            return dumplist
            
            
        sender = header["from"]
        recipient = header["to"]
        cc = header["cc"]
        bcc = header["bcc"]
        subject = header["subject"]
        date_object = header["date"]
        unique_hash = self.createMailHash(header)        
        print("header", header)
        print("hash", unique_hash)
        
        attachments = jsondump_list(attachments)
        if recipient == "":
            recipient = "None"
        if cc == "":
            cc = "None"
        if bcc == "":
            bcc = "None"
              
        self.dbquery.prepare("INSERT INTO mails (MailHash, Subject, Sender, Recipient, CC_recp, BCC_recp, receive_time, attachments, account, folder, Message) VALUES (?,?,?,?,?,?,?,?,?,?,?)")
        self.dbquery.addBindValue(unique_hash)
        self.dbquery.addBindValue(subject)
        self.dbquery.addBindValue(sender)
        self.dbquery.addBindValue(recipient)
        self.dbquery.addBindValue(cc)
        self.dbquery.addBindValue(bcc)
        self.dbquery.addBindValue(QtCore.QDateTime(date_object))
        self.dbquery.addBindValue(attachments)
        self.dbquery.addBindValue(account)
        self.dbquery.addBindValue(localFolder)
        self.dbquery.addBindValue(messageBody)
        
        self.dbquery.exec_()
        self.database.commit()
        print("commited")
        
        
        
        
    def createMailHash(self,header):
        hash_string = str(header["date"]) + header["subject"] + ''.join(header["from_only_email"] + header["to_only_email"] + header["cc_only_email"] + header["bcc_only_email"])
                
        unique_hash = hashlib.sha512(hash_string.encode("utf-8")).hexdigest()
        return unique_hash
        
    def checkMailExist(self,unique_hash):
        self.dbquery.prepare("SELECT MailID FROM mails WHERE MailHash=?")
        self.dbquery.addBindValue(unique_hash)
        self.dbquery.exec_()
        
        return self.dbquery.next()
                
    def parseMessage(self, message):    
        attachments = []
        try:
            if message.is_multipart():
                payload = message.get_payload()
                for part in payload:
                    if part.get_content_type().find("text") != -1:
                        if part.get_content_charset():
                            messageBody = part.get_payload(decode=True).decode(part.get_content_charset(), "replace")
                        else:
                            messageBody = part.get_payload(decode=False)
                    elif part.get_content_disposition() == "attachment":
                        filename = part.get_filename()
                        attachments.append(filename)
                    elif part.is_multipart():
                        messageBody, attachments = self.parseMessage(part)
                        
            elif message.get_content_type().find("text") != -1:
                    if message.get_content_charset():
                        messageBody = message.get_payload(decode=True).decode(message.get_content_charset(), "replace")
                    else:
                        messageBody = message.get_payload(decode=False)
            else:
                print(message.get_content_type())
            messageBody = messageBody.replace("\r","").replace("\n","")
        except:
            messageBody = "Message cannot be previewed here. Please open in external program."
        return messageBody, attachments
        
    @pyqtSlot()
    def on_btn_import_folder_clicked(self):
        path = str(QFileDialog.getExistingDirectory(self, "Select Directory")).replace("\\","/")
        if path == "":
            return
        self.line_import_source_folder.setText(path)
        folder = path.rsplit("/",1)[1]
        self.line_import_save_folder.setText(folder)
        
        print(path)
        
        
    @pyqtSlot()
    def on_btn_file_import_clicked(self):
        path = self.line_import_source_folder.text()
        account = self.line_import_account.text()
        localFolder = self.line_import_save_folder.text()
        
        if not os.path.isdir(path) or "" in [path.strip(), account.strip(), localFolder.strip()]:
            QMessageBox.warning(self, 'Incomplete information', 'Please fill all fields and make sure the provided path exists.', QMessageBox.Ok)
            return
        
        path = path.replace("\\","/")
        if not path.endswith("/"):
            path += "/"
        
        self.label_progress_folder.setText("Processing Folder: " + localFolder)
        mailTotal = 0
        for file in os.listdir(path):
            if file.endswith(".eml"):
                mailTotal += 1
        self.pBar_progress_import.setMinimum(0)
        self.pBar_progress_import.setMaximum(mailTotal)
        self.pBar_progress_import.setValue(0)
        mailCount = 0
        newMailCount = 0
        failedCount = 0
        
        for file in os.listdir(path):
            if file.endswith(".eml"):
                mailCount += 1
                self.label_progress_email.setText("E-Mail: %s/%s" % (mailCount, mailTotal))
                self.pBar_progress_import.setValue(mailCount)
                QCoreApplication.processEvents()
                print("working on file", file)
                try:
                    message = email.message_from_file(open(path + file,"r"))
                except:
                    failedCount += 1
                    continue
                
                
                header = self.getMessageHeader(message)
                hash = self.createMailHash(header)
                if self.checkMailExist(hash):
                    print("SKIPPING MAIL SKIPPING MAIL SKIPPING MAIL SKIPPING MAIL SKIPPING MAIL SKIPPING MAIL SKIPPING MAIL SKIPPING MAIL SKIPPING MAIL SKIPPING MAIL")
                    continue
                
                newMailCount += 1
                self.saveMailToDatabase(message, account, localFolder)
               
                
                # get MailID from database
                self.dbquery.prepare("SELECT MailID FROM mails WHERE MailHash=?")
                self.dbquery.addBindValue(hash)
                self.dbquery.exec_()
                self.dbquery.next()
                MailID = self.dbquery.value(0)
                
                # generate savepath and save mail to file
                savename = self.constructFilePath(MailID)
                savepath = savename.rsplit("/",1)[0]
                if not os.path.exists(savepath):
                    os.makedirs(savepath)
                shutil.copyfile(path + file, savename)
                    
        self.label_progress_folder.setText("Finished succesfully!")
        self.label_progress_email.setText("%s new Mails, %s failed" % (newMailCount, failedCount))
                
    def constructFilePath(self,MailID):
        self.dbquery.prepare("SELECT MailHash, account, folder FROM mails WHERE MailID=?")
        self.dbquery.addBindValue(MailID)
        self.dbquery.exec_()
        self.dbquery.next()
        
        hash = self.dbquery.value(0)
        account = self.dbquery.value(1)
        folder = self.dbquery.value(2)
        print("acc", account)
        print("folder", folder)
        print("hash", hash)
        return self.archiveBasePath + account + "/" + folder + "/" + hash + ".eml"
        
    @pyqtSlot()
    def on_btnResetView_clicked(self):
        print("reseting")
                
        self.line_filter_all.setText("")
        self.line_filter_subject.setText("")
        self.line_filter_from.setText("")
        self.line_filter_to.setText("")
        self.cBox_filter_cc.setChecked(True)
        self.cBox_filter_bcc.setChecked(True)
        self.line_filter_timestamp.setText("")
        self.cBox_filter_attachment_yes.setChecked(True)
        self.cBox_filter_attachment_no.setChecked(True)
        self.line_filter_attachments.setText("")
        self.cBox_filter_account.setCurrentIndex(0)
        self.cBox_filter_folder.setCurrentIndex(0)
        self.line_filter_message.setText("")
        
        self.model_mails.setQuery("SELECT * from mails ORDER BY receive_time DESC")
        
    def closeEvent(self,event):
        self.database.commit()
        self.database.close()
        event.accept()
                                                    
def main():
    app = QApplication(sys.argv)
    form = pyMailArchiver()
    form.show()
    app.exec_()

if __name__ == '__main__':
    main()
